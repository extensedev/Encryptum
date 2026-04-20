using System.Collections.Generic;

namespace Encryptum.Models;

public class VirtualFolder : VirtualNode
{
    public List<VirtualFolder> Folders { get; set; } = [];
    public List<VirtualFile> Files { get; set; } = [];
}