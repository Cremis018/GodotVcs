using System.Collections.Concurrent;

namespace GodotVcs.Lib;

/// <summary>
/// 单线程实体管理器（Godot环境下推荐用这个）
/// </summary>
public class EntityManager
{
    #region field
    // 储存每个索引的版本号
    private readonly List<uint> _versions = [];

    // 可回收的索引队列
    private readonly Queue<uint> _freeIndices = [];

    // 当前活跃的实体数量
    private uint _activeEntityCount;

    // 下一个索引，索引从1开始，0表示无效索引
    private uint _nextIndex = 1;
    #endregion

    #region prop
    /// <summary>
    /// 当前活跃实体数量
    /// </summary>
    public uint ActiveEntityCount => _activeEntityCount;
    #endregion

    #region op
    /// <summary>
    /// 创建新实体
    /// </summary>
    /// <returns>新实体</returns>
    public Entity Create()
    {
        uint index;
        uint version;

        if (_freeIndices.Count > 0)
        {
            // 使用回收的索引
            index = _freeIndices.Dequeue();
            version = _versions[(int)index];
        }
        else
        {
            // 分配新索引
            index = _nextIndex++;
            version = 0;

            // 扩展版本数组
            while (_versions.Count <= index) _versions.Add(0);
        }

        _versions[(int)index] = version;
        _activeEntityCount++;

        return new Entity(index, version);
    }

    /// <summary>
    /// 批量创建新实体
    /// </summary>
    /// <param name="count">实体数量</param>
    /// <returns>实体数组</returns>
    public Entity[] Create(int count)
    {
        if (count <= 0)
            throw new ArgumentException("参数count必须大于0", nameof(count));

        var entities = new Entity[count];

        // 批量创建时不需要锁，直接循环调用
        for (int i = 0; i < count; i++) entities[i] = Create();

        return entities;
    }

    /// <summary>
    /// 销毁实体
    /// </summary>
    /// <param name="entity">被销毁的实体</param>
    /// <returns>是否操作成功</returns>
    public bool Destroy(Entity entity)
    {
        if (!IsValid(entity))
            return false;

        uint index = entity.Index;

        // 检查版本是否匹配
        if (index >= _versions.Count || _versions[(int)index] != entity.Version)
            return false;

        // 增加版本号并回收索引
        _versions[(int)index]++;
        _freeIndices.Enqueue(index);
        _activeEntityCount--;

        return true;
    }

    /// <summary>
    /// 批量销毁实体
    /// </summary>
    /// <param name="entities">实体数组</param>
    /// <returns>销毁的实体数量</returns>
    public int Destroy(Entity[]? entities)
    {
        if (entities == null || entities.Length == 0)
            return 0;

        int destroyed = 0;

        // 使用循环而不是LINQ，避免额外开销
        foreach (var entity in entities)
        {
            if (Destroy(entity))
            {
                destroyed++;
            }
        }

        return destroyed;
    }

    /// <summary>
    /// 清空所有实体 <b>（谨慎使用！）</b>
    /// </summary>
    public void Clear()
    {
        _versions.Clear();
        _freeIndices.Clear();
        _activeEntityCount = 0;
        _nextIndex = 1;
    }
    #endregion

    #region debug
    /// <summary>
    /// 获取实体的版本号
    /// </summary>
    /// <param name="entity">目标实体</param>
    /// <returns>版本号</returns>
    public uint GetVersion(Entity entity)
    {
        if (!entity.IsValid)
            return 0;

        uint index = entity.Index;

        return index >= _versions.Count ? 0 : _versions[(int)index];
    }

    /// <summary>
    /// 统计实体管理器的信息
    /// </summary>
    /// <returns>实体管理器的信息</returns>
    public EntityManagerStats GetStats()
    {
        return new EntityManagerStats
        {
            ActiveEntityCount = _activeEntityCount,
            FreeIndexCount = (uint)_freeIndices.Count,
            NextIndex = _nextIndex,
            TotalCreated = _nextIndex - 1
        };
    }
    #endregion

    #region judge
    /// <summary>
    /// 检查实体是否有效
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>是否有效</returns>
    public bool IsValid(Entity entity)
    {
        if (!entity.IsValid)
            return false;

        uint index = entity.Index;

        if (index >= _versions.Count)
            return false;

        return _versions[(int)index] == entity.Version;
    }
    #endregion
}

/// <summary>
/// 无锁实体管理器（后台线程版本）
/// 使用原子操作和线程安全集合，适用于多线程环境
/// 适合物理计算、AI等后台线程场景
/// </summary>
public class LockFreeEntityManager
{
    #region field
    // 使用线程安全的并发字典存储版本号
    private readonly ConcurrentDictionary<uint, uint> _versions = new();

    // 使用线程安全的并发队列存储可回收索引
    private readonly ConcurrentQueue<uint> _freeIndices = new();

    // 使用Interlocked进行原子操作的计数器
    private long _activeEntityCount;
    private long _nextIndex = 1; // 使用long以支持Interlocked操作
    #endregion

    #region prop
    /// <summary>
    /// 当前活跃实体数量（原子读取）
    /// </summary>
    public uint ActiveEntityCount => (uint)Interlocked.Read(ref _activeEntityCount);
    #endregion

    #region op
    /// <summary>
    /// 创建新实体（无锁，线程安全）
    /// </summary>
    /// <returns>新实体</returns>
    public Entity Create()
    {
        uint version;

        // 尝试从回收队列获取索引（无锁操作）
        if (_freeIndices.TryDequeue(out var index))
        {
            // 原子更新版本号
            version = _versions.AddOrUpdate(
                index,
                1, // 如果不存在，初始值为1
                (_, oldValue) => oldValue + 1 // 如果存在，递增版本号
            );
        }
        else
        {
            // 原子递增获取新索引
            index = (uint)Interlocked.Increment(ref _nextIndex) - 1;
            version = 0;

            // 尝试添加版本号（如果已存在则获取）
            _versions.TryAdd(index, version);
        }

        // 原子递增活跃实体计数
        Interlocked.Increment(ref _activeEntityCount);

        return new Entity(index, version);
    }

    /// <summary>
    /// 批量创建新实体（无锁，线程安全）
    /// </summary>
    /// <param name="count">实体数量</param>
    /// <returns>实体数组</returns>
    public Entity[] Create(int count)
    {
        if (count <= 0)
            throw new ArgumentException("参数count必须大于0", nameof(count));

        var entities = new Entity[count];

        // 批量创建，每个Create调用都是无锁的
        for (int i = 0; i < count; i++)
        {
            entities[i] = Create();
        }

        return entities;
    }

    /// <summary>
    /// 销毁实体（无锁，线程安全）
    /// </summary>
    /// <param name="entity">被销毁的实体</param>
    /// <returns>是否操作成功</returns>
    public bool Destroy(Entity entity)
    {
        if (!entity.IsValid)
            return false;

        uint index = entity.Index;

        // 无锁读取版本号
        if (!_versions.TryGetValue(index, out uint currentVersion))
            return false;

        // 检查版本是否匹配（无锁读取）
        if (currentVersion != entity.Version)
            return false;

        // 回收索引到队列（无锁操作）
        _freeIndices.Enqueue(index);

        // 原子递减活跃实体计数
        Interlocked.Decrement(ref _activeEntityCount);

        // 注意：版本号递增在下次Create时进行，避免并发问题
        // 这里不立即递增版本号，而是在Create时通过AddOrUpdate处理

        return true;
    }

    /// <summary>
    /// 批量销毁实体（无锁，线程安全）
    /// </summary>
    /// <param name="entities">实体数组</param>
    /// <returns>销毁的实体数量</returns>
    public int Destroy(Entity[]? entities)
    {
        if (entities == null || entities.Length == 0)
            return 0;

        // 使用循环批量销毁
        return entities.Count(Destroy);
    }

    /// <summary>
    /// 清空所有实体 <b>（谨慎使用！）</b>
    /// </summary>
    public void Clear()
    {
        // 清空并发集合
        _versions.Clear();

        // 清空回收队列
        while (_freeIndices.TryDequeue(out _))
        {
        }

        // 原子重置计数器
        Interlocked.Exchange(ref _nextIndex, 1);
        Interlocked.Exchange(ref _activeEntityCount, 0);
    }
    #endregion

    #region debug
    /// <summary>
    /// 获取实体的版本号（无锁读取）
    /// </summary>
    /// <param name="entity">目标实体</param>
    /// <returns>版本号</returns>
    public uint GetVersion(Entity entity)
    {
        if (!entity.IsValid)
            return 0;

        uint index = entity.Index;

        return _versions.TryGetValue(index, out uint version) ? version : 0;
    }

    /// <summary>
    /// 统计实体管理器的信息（无锁读取）
    /// </summary>
    /// <returns>实体管理器的信息</returns>
    public EntityManagerStats GetStats()
    {
        // 注意：这些统计信息是近似值，因为是无锁读取
        return new EntityManagerStats
        {
            ActiveEntityCount = ActiveEntityCount,
            FreeIndexCount = (uint)_freeIndices.Count, // 近似值
            NextIndex = (uint)Interlocked.Read(ref _nextIndex),
            TotalCreated = (uint)Interlocked.Read(ref _nextIndex) - 1
        };
    }
    #endregion

    #region judge
    /// <summary>
    /// 检查实体是否有效（无锁读取）
    /// </summary>
    /// <param name="entity">实体</param>
    /// <returns>是否有效</returns>
    public bool IsValid(Entity entity)
    {
        if (!entity.IsValid)
            return false;

        uint index = entity.Index;

        // 无锁读取版本号
        if (!_versions.TryGetValue(index, out uint currentVersion))
            return false;

        return currentVersion == entity.Version;
    }
    #endregion
}

/// <summary>
/// 实体管理器的统计信息
/// </summary>
public struct EntityManagerStats
{
    public uint ActiveEntityCount;
    public uint FreeIndexCount;
    public uint NextIndex;
    public uint TotalCreated;
}