"""
desktop_integration.py  —  Windows masaüstü simge gizleme (DENEYSEL)
====================================================================

Gerçek "Fences" mantığı: Bir masaüstü simgesini, dosyayı DİSKTE TAŞIMADAN
masaüstünden gizler. Teknik olarak simge, Explorer'ın masaüstü liste
görünümünde (SysListView32) ekranın çok dışına taşınır; böylece görünmez
olur ama dosya/klasör yerinde kalır. Geri getirince eski konumuna döner.

ÖNEMLİ UYARILAR
---------------
* Yalnızca **Windows**'ta çalışır (ctypes + Win32 API). Başka platformda
  `DesktopIcons.available = False` olur ve hiçbir şey yapmaz.
* Masaüstünde sağ tık → Görünüm → **"Simgeleri otomatik düzenle" KAPALI**
  ve tercihen **"Simgeleri ızgaraya hizala" KAPALI** olmalıdır. Açıksa
  Explorer simgeyi hemen geri taşır ve gizleme çalışmaz.
* 64-bit Windows'ta 64-bit Python kullanın (Explorer 64-bit'tir). Bit
  uyuşmazlığında yapı boyutları tutmaz.
* Bu, işletim sistemine müdahale eden düşük seviyeli bir numaradır; Explorer
  yenilenince (F5) veya oturum değişince simgeler geri gelebilir. Bu yüzden
  program açılışta gizli öğeleri yeniden gizler.

Kullanım:
    di = DesktopIcons()
    if di.available:
        old = di.hide("Klasör Adı")     # (x, y) döner veya None
        di.show("Klasör Adı", old)      # geri getirir
"""

import sys
import ctypes
from ctypes import wintypes

IS_WINDOWS = sys.platform.startswith("win")

# Ekran dışına park konumu (simgeyi görünmez kılmak için)
_PARK_X = -32000
_PARK_Y = -32000

# ListView mesajları
_LVM_FIRST = 0x1000
_LVM_GETITEMCOUNT = _LVM_FIRST + 4
_LVM_GETITEMPOSITION = _LVM_FIRST + 16
_LVM_SETITEMPOSITION32 = _LVM_FIRST + 49
_LVM_GETITEMTEXTW = _LVM_FIRST + 115

# Pencere stili
_GWL_STYLE = -16
_LVS_AUTOARRANGE = 0x0100

# Process erişimi
_PROCESS_VM_OPERATION = 0x0008
_PROCESS_VM_READ = 0x0010
_PROCESS_VM_WRITE = 0x0020
_PROCESS_QUERY_INFORMATION = 0x0400

_MEM_COMMIT = 0x1000
_MEM_RESERVE = 0x2000
_MEM_RELEASE = 0x8000
_PAGE_READWRITE = 0x04


class _POINT(ctypes.Structure):
    _fields_ = [("x", ctypes.c_long), ("y", ctypes.c_long)]


class _LVITEMW(ctypes.Structure):
    # 64-bit hizalamayı ctypes otomatik yapar (pointer alanları 8 bayt).
    _fields_ = [
        ("mask", wintypes.UINT),
        ("iItem", ctypes.c_int),
        ("iSubItem", ctypes.c_int),
        ("state", wintypes.UINT),
        ("stateMask", wintypes.UINT),
        ("pszText", ctypes.c_void_p),
        ("cchTextMax", ctypes.c_int),
        ("iImage", ctypes.c_int),
        ("lParam", ctypes.c_void_p),
        ("iIndent", ctypes.c_int),
        ("iGroupId", ctypes.c_int),
        ("cColumns", wintypes.UINT),
        ("puColumns", ctypes.c_void_p),
        ("piColFmt", ctypes.c_void_p),
        ("iGroup", ctypes.c_int),
    ]


class DesktopIcons:
    """Masaüstü simgelerini ada göre gizler/geri getirir."""

    def __init__(self):
        self.available = False
        self._hproc = None
        self._remote = None
        self._lv = None
        if not IS_WINDOWS:
            return
        try:
            self._setup()
            self.available = self._lv is not None and self._remote is not None
        except Exception:
            self.available = False

    # -- Kurulum -------------------------------------------------------------
    def _setup(self):
        self._user32 = ctypes.WinDLL("user32", use_last_error=True)
        self._kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

        u, k = self._user32, self._kernel32

        u.FindWindowW.restype = wintypes.HWND
        u.FindWindowW.argtypes = [wintypes.LPCWSTR, wintypes.LPCWSTR]
        u.FindWindowExW.restype = wintypes.HWND
        u.FindWindowExW.argtypes = [wintypes.HWND, wintypes.HWND,
                                    wintypes.LPCWSTR, wintypes.LPCWSTR]
        u.SendMessageW.restype = ctypes.c_ssize_t
        u.SendMessageW.argtypes = [wintypes.HWND, wintypes.UINT,
                                   ctypes.c_size_t, ctypes.c_ssize_t]
        u.GetWindowThreadProcessId.restype = wintypes.DWORD
        u.GetWindowThreadProcessId.argtypes = [wintypes.HWND,
                                               ctypes.POINTER(wintypes.DWORD)]
        u.GetWindowLongW.restype = ctypes.c_long
        u.GetWindowLongW.argtypes = [wintypes.HWND, ctypes.c_int]

        k.OpenProcess.restype = wintypes.HANDLE
        k.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        k.VirtualAllocEx.restype = ctypes.c_void_p
        k.VirtualAllocEx.argtypes = [wintypes.HANDLE, ctypes.c_void_p,
                                     ctypes.c_size_t, wintypes.DWORD,
                                     wintypes.DWORD]
        k.VirtualFreeEx.restype = wintypes.BOOL
        k.VirtualFreeEx.argtypes = [wintypes.HANDLE, ctypes.c_void_p,
                                    ctypes.c_size_t, wintypes.DWORD]
        k.WriteProcessMemory.restype = wintypes.BOOL
        k.WriteProcessMemory.argtypes = [wintypes.HANDLE, ctypes.c_void_p,
                                         ctypes.c_void_p, ctypes.c_size_t,
                                         ctypes.POINTER(ctypes.c_size_t)]
        k.ReadProcessMemory.restype = wintypes.BOOL
        k.ReadProcessMemory.argtypes = [wintypes.HANDLE, ctypes.c_void_p,
                                        ctypes.c_void_p, ctypes.c_size_t,
                                        ctypes.POINTER(ctypes.c_size_t)]
        k.CloseHandle.restype = wintypes.BOOL
        k.CloseHandle.argtypes = [wintypes.HANDLE]

        self._lv = self._find_listview()
        if not self._lv:
            return

        pid = wintypes.DWORD(0)
        u.GetWindowThreadProcessId(self._lv, ctypes.byref(pid))
        access = (_PROCESS_VM_OPERATION | _PROCESS_VM_READ |
                  _PROCESS_VM_WRITE | _PROCESS_QUERY_INFORMATION)
        self._hproc = k.OpenProcess(access, False, pid.value)
        if not self._hproc:
            return
        # Tek bir uzak tampon: 0..2047 LVITEM/POINT, 2048.. metin
        self._remote = k.VirtualAllocEx(
            self._hproc, None, 4096, _MEM_COMMIT | _MEM_RESERVE,
            _PAGE_READWRITE)

    def _find_listview(self):
        u = self._user32
        # Normal durum: Progman > SHELLDLL_DefView > SysListView32
        progman = u.FindWindowW("Progman", None)
        defview = u.FindWindowExW(progman, None, "SHELLDLL_DefView", None)
        if not defview:
            # Duvar kağıdı slaytı açıkken defview bir WorkerW altındadır.
            workerw = None
            while True:
                workerw = u.FindWindowExW(None, workerw, "WorkerW", None)
                if not workerw:
                    break
                defview = u.FindWindowExW(workerw, None,
                                          "SHELLDLL_DefView", None)
                if defview:
                    break
        if not defview:
            return None
        return u.FindWindowExW(defview, None, "SysListView32", None)

    def auto_arrange_on(self):
        """Otomatik düzenleme açıksa gizleme çalışmaz — uyarmak için."""
        if not self.available:
            return False
        style = self._user32.GetWindowLongW(self._lv, _GWL_STYLE)
        return bool(style & _LVS_AUTOARRANGE)

    # -- Dahili okuma/yazma --------------------------------------------------
    def _count(self):
        return self._user32.SendMessageW(self._lv, _LVM_GETITEMCOUNT, 0, 0)

    def _text(self, index):
        text_addr = self._remote + 2048
        item = _LVITEMW()
        item.mask = 0
        item.iItem = index
        item.iSubItem = 0
        item.pszText = text_addr
        item.cchTextMax = 260
        written = ctypes.c_size_t(0)
        self._kernel32.WriteProcessMemory(
            self._hproc, self._remote, ctypes.byref(item),
            ctypes.sizeof(item), ctypes.byref(written))
        self._user32.SendMessageW(self._lv, _LVM_GETITEMTEXTW, index,
                                  self._remote)
        buf = (ctypes.c_wchar * 260)()
        read = ctypes.c_size_t(0)
        self._kernel32.ReadProcessMemory(
            self._hproc, text_addr, buf, 520, ctypes.byref(read))
        return buf.value

    def _get_pos(self, index):
        pos_addr = self._remote + 1024
        self._user32.SendMessageW(self._lv, _LVM_GETITEMPOSITION, index,
                                  pos_addr)
        pt = _POINT()
        read = ctypes.c_size_t(0)
        self._kernel32.ReadProcessMemory(
            self._hproc, pos_addr, ctypes.byref(pt), ctypes.sizeof(pt),
            ctypes.byref(read))
        return pt.x, pt.y

    def _set_pos(self, index, x, y):
        pos_addr = self._remote + 1024
        pt = _POINT(x, y)
        written = ctypes.c_size_t(0)
        self._kernel32.WriteProcessMemory(
            self._hproc, pos_addr, ctypes.byref(pt), ctypes.sizeof(pt),
            ctypes.byref(written))
        self._user32.SendMessageW(self._lv, _LVM_SETITEMPOSITION32, index,
                                  pos_addr)

    def _find_index(self, name):
        """İsmi (veya uzantısız halini) eşleşen ilk simgenin indeksi."""
        target = name.casefold()
        stem = target.rsplit(".", 1)[0] if "." in target else target
        count = self._count()
        for i in range(count):
            label = self._text(i).casefold()
            if label == target or label == stem:
                return i
        return None

    # -- Genel API -----------------------------------------------------------
    def hide(self, name):
        """Masaüstü simgesini gizler. Eski (x, y) konumunu döndürür."""
        if not self.available:
            return None
        try:
            idx = self._find_index(name)
            if idx is None:
                return None
            old = self._get_pos(idx)
            self._set_pos(idx, _PARK_X, _PARK_Y)
            return old
        except Exception:
            return None

    def show(self, name, pos=None):
        """Simgeyi geri getirir; pos verilmişse o konuma, yoksa (0,0)'a."""
        if not self.available:
            return False
        try:
            idx = self._find_index(name)
            if idx is None:
                return False
            x, y = pos if pos else (0, 0)
            self._set_pos(idx, x, y)
            return True
        except Exception:
            return False

    def close(self):
        try:
            if self._remote and self._hproc:
                self._kernel32.VirtualFreeEx(
                    self._hproc, self._remote, 0, _MEM_RELEASE)
            if self._hproc:
                self._kernel32.CloseHandle(self._hproc)
        except Exception:
            pass
        finally:
            self._remote = None
            self._hproc = None
            self.available = False
