using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
namespace CIDE.Helpers;

public static class Win32FileDialog
{
    private const uint FOS_PICKFOLDERS = 0x00000020;
    private const uint FOS_FORCEFILESYSTEM = 0x00000040;
    private const uint SIGDN_FILESYSPATH = 0x80058000;
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct COMDLG_FILTERSPEC
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pszName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pszSpec;
    }
    public static string? ShowFolderPicker(IntPtr owner)
    {
        try
        {
            var dialog = (IFileOpenDialog)new FileOpenDialog();
            dialog.SetOptions(FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM);
            dialog.SetTitle("Выберите папку проекта");
            var hr = dialog.Show(owner);
            if (hr == 0)
            {
                dialog.GetResult(out var item);
                if (item != null)
                {
                    item.GetDisplayName(SIGDN_FILESYSPATH, out var path);
                    return path;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Win32 FolderPicker Error: {ex}");
        }
        return null;
    }
    public static string? ShowFilePicker(IntPtr owner, string title, string filterName, string filterSpec)
    {
        try
        {
            var dialog = (IFileOpenDialog)new FileOpenDialog();
            dialog.SetOptions(FOS_FORCEFILESYSTEM);
            dialog.SetTitle(title);
            var filters = new COMDLG_FILTERSPEC[]
            {
                new() { pszName = filterName, pszSpec = filterSpec },
                new() { pszName = "Все файлы (*.*)", pszSpec = "*.*" }
            };
            dialog.SetFileTypes((uint)filters.Length, filters);
            var hr = dialog.Show(owner);
            if (hr == 0)
            {
                dialog.GetResult(out var item);
                if (item != null)
                {
                    item.GetDisplayName(SIGDN_FILESYSPATH, out var path);
                    return path;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Win32 FilePicker Error: {ex}");
        }
        return null;
    }
    [ComImport]
    [Guid("42f85136-db7e-439c-8591-e4075d135fc8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig]
        public int Show([In] IntPtr parent);
        public void SetFileTypes([In] uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        public void SetFileTypeIndex([In] uint iFileType);
        public void GetFileTypeIndex(out uint piFileType);
        public void Advise([In, MarshalAs(UnmanagedType.Interface)] IntPtr pfde, out uint pdwCookie);
        public void Unadvise([In] uint dwCookie);
        public void SetOptions([In] uint fos);
        public void GetOptions(out uint pfos);
        public void SetDefaultFolder([In] IShellItem psi);
        public void SetFolder([In] IShellItem psi);
        public void GetFolder(out IShellItem ppsi);
        public void GetCurrentSelection(out IShellItem ppsi);
        public void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);
        public void GetFileName(out IntPtr pszName);
        public void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        public void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);
        public void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        public void GetResult(out IShellItem ppsi);
        public void AddPlace([In] IShellItem psi, int fdap);
        public void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        public void Close([In] int hr);
        public void SetClientGuid([In] ref Guid guid);
        public void ClearClientData();
        public void SetFilter([In] IntPtr pFilter);
    }
    [ComImport]
    [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog : IFileDialog
    {
        [PreserveSig]
        public new int Show([In] IntPtr parent);
        public new void SetFileTypes([In] uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        public new void SetFileTypeIndex([In] uint iFileType);
        public new void GetFileTypeIndex(out uint piFileType);
        public new void Advise([In, MarshalAs(UnmanagedType.Interface)] IntPtr pfde, out uint pdwCookie);
        public new void Unadvise([In] uint dwCookie);
        public new void SetOptions([In] uint fos);
        public new void GetOptions(out uint pfos);
        public new void SetDefaultFolder([In] IShellItem psi);
        public new void SetFolder([In] IShellItem psi);
        public new void GetFolder(out IShellItem ppsi);
        public new void GetCurrentSelection(out IShellItem ppsi);
        public new void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);
        public new void GetFileName(out IntPtr pszName);
        public new void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        public new void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);
        public new void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        public new void GetResult(out IShellItem ppsi);
        public new void AddPlace([In] IShellItem psi, int fdap);
        public new void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        public new void Close([In] int hr);
        public new void SetClientGuid([In] ref Guid guid);
        public new void ClearClientData();
        public new void SetFilter([In] IntPtr pFilter);
        public void GetResults(out object ppenum);
        public void GetSelectedItems(out object ppselitems);
    }
    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem
    {
        public void BindToHandler([In] IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
        public void GetParent(out IShellItem ppsi);
        public void GetDisplayName([In] uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        public void GetAttributes([In] uint sfgaoMask, out uint psfgaoAttribs);
        public void Compare([In] IShellItem psi, [In] uint hint, out int piOrder);
    }
    [ComImport]
    [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private class FileOpenDialog
    {
    }
}
