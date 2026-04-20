namespace Encryptum.Models;

public class VaultData
{
    public int Version { get; set; } = 1;
    public VirtualFolder Root { get; set; } = new() { Name = "Root" };
}