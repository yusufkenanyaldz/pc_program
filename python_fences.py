"""
Python Fences - Bağımsız Masaüstü Düzenleyici
=============================================

Masaüstünüzdeki programları, klasörleri ve dosyaları, birbirinden BAĞIMSIZ
açılır pencereler (fence) içinde düzenlemenizi sağlar.

ÖNEMLİ: Dosyalar FİZİKSEL OLARAK TAŞINMAZ. Pencereye sürüklenen her öğe
sadece "referans" (kısayol mantığı) olarak saklanır. Orijinal dosya/klasör
bulunduğu yerde (masaüstü, disk vb.) kalmaya devam eder. Böylece klasörleme
olmadan, istediğiniz kadar bağımsız pencere oluşturabilirsiniz.

Özellikler:
  - Birden fazla bağımsız pencere (her biri ayrı konum, başlık ve öğe listesi)
  - Program (.exe), klasör ve tüm dosya türleri için sürükle-bırak
  - Dosyalar taşınmaz, sadece referans tutulur
  - Pencere konumları ve öğeler otomatik kaydedilir
  - Başlığa tutup sürükleyerek pencereyi taşıma
  - Sağ tık menüsü: Aç / Konumunu Aç / Pencereden Çıkar / Diskten Sil

Gereksinimler:  pip install pillow tkinterdnd2
"""

import os
import sys
import json
import uuid
import subprocess

import tkinter as tk
from tkinter import messagebox, simpledialog

from PIL import Image, ImageDraw, ImageTk
from tkinterdnd2 import DND_FILES, TkinterDnD

# ---------------------------------------------------------------------------
# Tema / sabitler
# ---------------------------------------------------------------------------
THEME_BG = "#e0f2fe"
HEADER_BG = "#3498db"
HOVER_BG = "#b3e5fc"
TEXT_COLOR = "white"
ITEM_TEXT = "#2c3e50"

FONT_HEADER = ("Segoe UI", 11, "bold")
FONT_ITEM = ("Segoe UI", 8, "bold")
FONT_BTN = ("Segoe UI", 9, "bold")

COLUMNS = 4          # Izgaradaki sütun sayısı
DATA_FILE = "fences_data.json"


# ---------------------------------------------------------------------------
# Platformdan bağımsız "aç" işlevi
# ---------------------------------------------------------------------------
def open_path(path):
    """Bir dosyayı/klasörü/programı işletim sistemi varsayılanıyla açar."""
    if not os.path.exists(path):
        raise FileNotFoundError(f"Öğe bulunamadı:\n{path}")
    if sys.platform.startswith("win"):
        os.startfile(path)  # type: ignore[attr-defined]  # sadece Windows
    elif sys.platform == "darwin":
        subprocess.Popen(["open", path])
    else:
        subprocess.Popen(["xdg-open", path])


def reveal_path(path):
    """Öğenin bulunduğu klasörü açar (öğeyi silmeden konumunu gösterir)."""
    folder = path if os.path.isdir(path) else os.path.dirname(path)
    open_path(folder)


def detect_type(path):
    """Bir yolun ikon türünü belirler."""
    if os.path.isdir(path):
        return "folder"
    ext = os.path.splitext(path)[1].lower()
    if ext in (".py", ".pyw"):
        return "python"
    if ext in (".xls", ".xlsx", ".xlsm", ".csv"):
        return "excel"
    if ext in (".exe", ".bat", ".cmd", ".msi", ".lnk", ".app", ".sh"):
        return "program"
    if ext in (".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico"):
        return "image"
    return "file"


# ---------------------------------------------------------------------------
# İkon üretici
# ---------------------------------------------------------------------------
class IconGenerator:
    @staticmethod
    def create_icon(icon_type, size=32):
        img = Image.new("RGBA", (size, size), color=(255, 255, 255, 0))
        draw = ImageDraw.Draw(img)

        if icon_type == "folder":
            draw.rectangle([2, 9, 30, 28], fill="#f1c40f", outline="#f39c12")
            draw.rectangle([2, 5, 14, 11], fill="#f39c12")
        elif icon_type == "python":
            draw.ellipse([2, 2, 30, 30], fill="#3498db")
            draw.text((11, 8), "Py", fill="#f1c40f")
        elif icon_type == "excel":
            draw.rectangle([2, 5, 30, 28], fill="#27ae60")
            draw.text((12, 9), "X", fill="white")
        elif icon_type == "program":
            draw.rectangle([3, 3, 29, 29], fill="#8e44ad", outline="#6c3483")
            draw.polygon([(13, 10), (23, 16), (13, 22)], fill="white")
        elif icon_type == "image":
            draw.rectangle([3, 5, 29, 27], fill="#e67e22", outline="#d35400")
            draw.ellipse([7, 9, 13, 15], fill="#fdebd0")
            draw.polygon([(6, 25), (15, 15), (26, 25)], fill="#a04000")
        else:  # generic file
            draw.rectangle([6, 3, 26, 29], fill="white", outline="#7f8c8d")
            draw.line([10, 10, 22, 10], fill="#7f8c8d")
            draw.line([10, 14, 22, 14], fill="#7f8c8d")
            draw.line([10, 18, 22, 18], fill="#7f8c8d")

        return img


# ---------------------------------------------------------------------------
# Tek bir bağımsız "fence" penceresi
# ---------------------------------------------------------------------------
class FenceWindow(tk.Toplevel):
    def __init__(self, app, data):
        super().__init__(app.root)
        self.app = app
        self.data = data  # {"id", "title", "geometry", "items": [...]}

        self.overrideredirect(True)
        self.wm_attributes("-topmost", True)
        self.configure(bg=THEME_BG, highlightbackground=HEADER_BG,
                       highlightthickness=2)

        self.geometry(self.data.get("geometry", "320x400+200+120"))

        self._drag_x = 0
        self._drag_y = 0

        self._build_header()
        self._build_body()

        # Sürüklenen dosyaları bu pencereye bırakma.
        # Pencerenin HER yeri (ana pencere + ızgara + ipucu) bırakma hedefidir;
        # böylece boş pencerede ortadaki yazının üstüne bırakınca da algılanır.
        for target in (self, self.grid_frame, self.hint):
            target.drop_target_register(DND_FILES)
            target.dnd_bind("<<Drop>>", self.on_drop)

        self.refresh_grid()

        # Konum/boyut değişince kaydet
        self.bind("<Configure>", self._on_configure)

    # -- Arayüz --------------------------------------------------------------
    def _build_header(self):
        header = tk.Frame(self, bg=HEADER_BG, height=34)
        header.pack(fill="x", side="top")
        header.pack_propagate(False)

        self.title_label = tk.Label(
            header, text=self.data["title"], bg=HEADER_BG, fg=TEXT_COLOR,
            font=FONT_HEADER, anchor="w", padx=8)
        self.title_label.pack(side="left", fill="both", expand=True)

        # Pencereyi başlığından tutup taşıma
        for w in (header, self.title_label):
            w.bind("<Button-1>", self._start_move)
            w.bind("<B1-Motion>", self._do_move)
            w.bind("<Double-Button-1>", lambda e: self.rename())

        # Başlık butonları
        tk.Button(header, text="✕", bg="#e74c3c", fg="white", font=FONT_BTN,
                  relief="flat", width=3, command=self.close_fence
                  ).pack(side="right", fill="y")
        tk.Button(header, text="✎", bg=HEADER_BG, fg="white", font=FONT_BTN,
                  relief="flat", width=3, command=self.rename
                  ).pack(side="right", fill="y")

    def _build_body(self):
        self.grid_frame = tk.Frame(self, bg=THEME_BG, padx=10, pady=10)
        self.grid_frame.pack(fill="both", expand=True)
        for c in range(COLUMNS):
            self.grid_frame.grid_columnconfigure(c, weight=1)

        # Boş durum ipucu (ızgaranın içinde; böylece bırakma alanını örtmez)
        self.hint = tk.Label(
            self.grid_frame,
            text="Program, klasör veya dosyaları\nburaya sürükleyin",
            bg=THEME_BG, fg="#7f8c8d", font=("Segoe UI", 8, "italic"))

        footer = tk.Frame(self, bg=THEME_BG)
        footer.pack(fill="x", side="bottom", pady=(0, 6))
        tk.Button(footer, text="+ Yeni Pencere", bg=HEADER_BG, fg="white",
                  font=FONT_BTN, relief="flat", command=self.app.new_fence
                  ).pack(side="left", padx=10)

    # -- Pencere taşıma ------------------------------------------------------
    def _start_move(self, event):
        self._drag_x = event.x
        self._drag_y = event.y

    def _do_move(self, event):
        x = self.winfo_x() + (event.x - self._drag_x)
        y = self.winfo_y() + (event.y - self._drag_y)
        self.geometry(f"+{x}+{y}")

    def _on_configure(self, event):
        # Sadece bu pencerenin kendi olaylarında geometriyi güncelle
        if event.widget is self:
            self.data["geometry"] = self.geometry()
            self.app.schedule_save()

    # -- Öğe işlemleri -------------------------------------------------------
    def on_drop(self, event):
        paths = self._parse_drop(event.data)
        added = 0
        for path in paths:
            path = os.path.normpath(path)
            if not os.path.exists(path):
                continue
            # Aynı öğe zaten varsa tekrar ekleme
            if any(it["path"] == path for it in self.data["items"]):
                continue
            name = os.path.basename(path.rstrip(os.sep)) or path
            self.data["items"].append({
                "path": path,
                "name": os.path.splitext(name)[0] if not os.path.isdir(path) else name,
                "type": detect_type(path),
            })
            added += 1

        if added:
            self.refresh_grid()
            self.app.save_data()

    @staticmethod
    def _parse_drop(data):
        """tkinterdnd2 birden fazla dosyayı {a b}{c} şeklinde verebilir."""
        paths, buf, in_brace = [], "", False
        for ch in data:
            if ch == "{":
                in_brace, buf = True, ""
            elif ch == "}":
                in_brace = False
                paths.append(buf)
                buf = ""
            elif ch == " " and not in_brace:
                if buf:
                    paths.append(buf)
                    buf = ""
            else:
                buf += ch
        if buf:
            paths.append(buf)
        return [p for p in paths if p]

    def refresh_grid(self):
        for w in self.grid_frame.winfo_children():
            if w is not self.hint:
                w.destroy()

        if not self.data["items"]:
            self.hint.grid(row=0, column=0, columnspan=COLUMNS, pady=30)
        else:
            self.hint.grid_forget()
            for index, item in enumerate(self.data["items"]):
                self._add_item_widget(item, index)

    def _add_item_widget(self, item, index):
        frame = tk.Frame(self.grid_frame, bg=THEME_BG, padx=4, pady=4)
        frame.grid(row=index // COLUMNS, column=index % COLUMNS,
                   sticky="nsew", padx=2, pady=2)

        icon = self.app.icons.get(item["type"], self.app.icons["file"])
        icon_label = tk.Label(frame, image=icon, bg=THEME_BG)
        icon_label.pack(pady=(0, 4))

        name = item["name"]
        shown = name[:12] + "…" if len(name) > 12 else name
        text_label = tk.Label(frame, text=shown, bg=THEME_BG, fg=ITEM_TEXT,
                              font=FONT_ITEM, wraplength=80, justify="center")
        text_label.pack()

        missing = not os.path.exists(item["path"])
        if missing:
            text_label.configure(fg="#e74c3c")

        for w in (frame, icon_label, text_label):
            w.bind("<Double-Button-1>", lambda e, p=item["path"]: self.launch(p))
            w.bind("<Button-3>", lambda e, it=item: self._show_menu(e, it))
            w.bind("<Enter>", lambda e, f=frame: self._set_bg(f, HOVER_BG))
            w.bind("<Leave>", lambda e, f=frame: self._set_bg(f, THEME_BG))

    @staticmethod
    def _set_bg(frame, color):
        frame.configure(bg=color)
        for child in frame.winfo_children():
            child.configure(bg=color)

    def launch(self, path):
        try:
            open_path(path)
        except Exception as e:
            messagebox.showerror("Hata", f"Öğe açılamadı:\n{e}")

    # -- Sağ tık menüsü ------------------------------------------------------
    def _show_menu(self, event, item):
        menu = tk.Menu(self, tearoff=0)
        menu.add_command(label="Aç", command=lambda: self.launch(item["path"]))
        menu.add_command(label="Konumunu Aç",
                         command=lambda: self._reveal(item["path"]))
        menu.add_separator()
        menu.add_command(label="Pencereden Çıkar (dosya silinmez)",
                         command=lambda: self._remove_item(item))
        menu.add_command(label="Diskten Sil…",
                         command=lambda: self._delete_from_disk(item))
        menu.tk_popup(event.x_root, event.y_root)

    def _reveal(self, path):
        try:
            reveal_path(path)
        except Exception as e:
            messagebox.showerror("Hata", f"Konum açılamadı:\n{e}")

    def _remove_item(self, item):
        """Öğeyi sadece pencereden çıkarır. Orijinal dosyaya dokunmaz."""
        self.data["items"] = [
            it for it in self.data["items"] if it["path"] != item["path"]]
        self.refresh_grid()
        self.app.save_data()

    def _delete_from_disk(self, item):
        path = item["path"]
        if not messagebox.askyesno(
                "Diskten Sil",
                f"'{os.path.basename(path)}' KALICI olarak silinsin mi?\n\n"
                "Bu işlem dosyayı diskten tamamen kaldırır, geri alınamaz!"):
            return
        try:
            if os.path.isdir(path):
                import shutil
                shutil.rmtree(path)
            elif os.path.exists(path):
                os.remove(path)
            self._remove_item(item)
        except Exception as e:
            messagebox.showerror("Hata", f"Silinirken hata oluştu:\n{e}")

    # -- Yeniden adlandırma / kapatma ---------------------------------------
    def rename(self):
        new = simpledialog.askstring(
            "Pencere Adı", "Yeni pencere başlığı:",
            initialvalue=self.data["title"], parent=self)
        if new:
            self.data["title"] = new.strip()
            self.title_label.configure(text=self.data["title"])
            self.app.save_data()

    def close_fence(self):
        if len(self.app.fences) == 1:
            if not messagebox.askyesno(
                    "Son Pencere",
                    "Bu son pencere. Kapatmak programdan çıkar. Devam?"):
                return
        self.app.remove_fence(self)


# ---------------------------------------------------------------------------
# Uygulama yöneticisi
# ---------------------------------------------------------------------------
class PythonFencesApp:
    def __init__(self):
        # tkinterdnd2 için kök pencere gizli tutulur; fence'ler Toplevel olur.
        self.root = TkinterDnD.Tk()
        self.root.withdraw()

        self.data_file = os.path.join(os.getcwd(), DATA_FILE)
        self.icons = self._load_icons()
        self.fences = []
        self._save_job = None

        fences_data = self._load_data()
        if not fences_data:
            fences_data = [self._default_fence()]

        for fd in fences_data:
            self._open_fence(fd)

    # -- İkonlar -------------------------------------------------------------
    def _load_icons(self):
        icons = {}
        for t in ("folder", "python", "excel", "program", "image", "file"):
            icons[t] = ImageTk.PhotoImage(IconGenerator.create_icon(t))
        return icons

    # -- Veri ----------------------------------------------------------------
    @staticmethod
    def _default_fence():
        sw = 320
        return {
            "id": uuid.uuid4().hex,
            "title": "ARAÇLAR",
            "geometry": f"320x400+{max(sw, 100)}+80",
            "items": [],
        }

    def _load_data(self):
        if not os.path.exists(self.data_file):
            return []
        try:
            with open(self.data_file, "r", encoding="utf-8") as f:
                raw = json.load(f)
        except Exception:
            return []

        # Eski biçim (düz öğe listesi) desteği -> tek pencereye çevir
        if isinstance(raw, list):
            if raw and isinstance(raw[0], dict) and "items" in raw[0]:
                return raw  # zaten fence listesi
            fence = self._default_fence()
            for it in raw:
                if isinstance(it, dict) and it.get("path"):
                    it.setdefault("type", detect_type(it["path"]))
                    fence["items"].append(it)
            return [fence]
        if isinstance(raw, dict) and "fences" in raw:
            return raw["fences"]
        return []

    def save_data(self):
        payload = {"fences": [f.data for f in self.fences]}
        try:
            with open(self.data_file, "w", encoding="utf-8") as f:
                json.dump(payload, f, ensure_ascii=False, indent=4)
        except Exception as e:
            messagebox.showerror("Hata", f"Veri kaydedilemedi:\n{e}")

    def schedule_save(self):
        """Configure olayları çok sık tetiklendiği için kaydı geciktirir."""
        if self._save_job is not None:
            self.root.after_cancel(self._save_job)
        self._save_job = self.root.after(600, self.save_data)

    # -- Fence yönetimi ------------------------------------------------------
    def _open_fence(self, fence_data):
        fence = FenceWindow(self, fence_data)
        self.fences.append(fence)
        return fence

    def new_fence(self):
        fd = self._default_fence()
        fd["title"] = "YENİ PENCERE"
        # Yeni pencereyi biraz kaydırarak aç
        offset = 30 * len(self.fences)
        fd["geometry"] = f"320x400+{260 + offset}+{80 + offset}"
        self._open_fence(fd)
        self.save_data()

    def remove_fence(self, fence):
        fence.destroy()
        self.fences = [f for f in self.fences if f is not fence]
        self.save_data()
        if not self.fences:
            self.root.quit()

    def run(self):
        self.root.mainloop()


if __name__ == "__main__":
    PythonFencesApp().run()
