namespace GodotVcs.Lib;

/// <summary>
/// 实体
/// </summary>
public readonly struct Entity : IEquatable<Entity>
{
    #region field
    private readonly ulong _id;
    #endregion
    
    #region const
    private const ulong IndexMask = 0x00000000FFFFFFFFUL;
    #endregion
    
    #region prop
    /// <summary>
    /// 获取实体索引
    /// </summary>
    public uint Index => (uint)(_id & IndexMask);
    /// <summary>
    /// 获取实体版本
    /// </summary>
    public uint Version => (uint)(_id >> 32 & IndexMask);
    /// <summary>
    /// 实体是否有效（我们设定当唯一标识符为0即代表无效）
    /// </summary>
    public bool IsValid => Index > 0;
    /// <summary>
    /// 无效实体
    /// </summary>
    public static readonly Entity Invalid = new(0);
    #endregion

    #region ctor
    /// <summary>
    /// 创建实体ID
    /// </summary>
    /// <param name="index">索引</param>
    /// <param name="version">版本</param>
    public Entity(uint index, uint version) => _id = ((ulong)version << 32) | index;

    /// <summary>
    /// 从原始ID创建
    /// </summary>
    /// <param name="id">唯一标识符</param>
    private Entity(ulong id) => _id = id;
    #endregion

    #region impl
    public override bool Equals(object? obj) => obj is Entity entity && Equals(entity);

    public bool Equals(Entity other) => _id == other._id;

    public override int GetHashCode() => _id.GetHashCode();

    public override string ToString() => $"Entity({Index}, {Version})";
    
    public static bool operator ==(Entity left, Entity right) => left.Equals(right);
    public static bool operator !=(Entity left, Entity right) => !(left == right);
    #endregion
}