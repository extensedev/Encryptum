using System;

namespace Encryptum.Models;

public abstract class VirtualNode
{
    public string Name { get; set; } = string.Empty;
    public Guid Id { get; set; } = Guid.NewGuid();
}