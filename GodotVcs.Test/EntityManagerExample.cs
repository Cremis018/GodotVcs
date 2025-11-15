using GodotVcs.Lib;

namespace GodotVcs.Test;

public class EntityManagerExample
{
    public static void RunExample()
    {
        var entityManager = new EntityManager();

        // 创建实体
        Entity player = entityManager.Create();
        Entity enemy1 = entityManager.Create();
        Entity enemy2 = entityManager.Create();

        Console.WriteLine($"创建了实体: {player}, {enemy1}, {enemy2}");
        Console.WriteLine($"活跃实体数: {entityManager.ActiveEntityCount}");

        // 批量创建
        Entity[] bullets = entityManager.Create(10);
        Console.WriteLine($"批量创建了 {bullets.Length} 个子弹实体");

        // 验证实体
        Console.WriteLine($"Player有效: {entityManager.IsValid(player)}");

        // 销毁实体
        entityManager.Destroy(enemy1);
        Console.WriteLine($"销毁enemy1后，活跃实体数: {entityManager.ActiveEntityCount}");

        // 尝试使用已销毁的实体（应该失败）
        Entity fakeEnemy = new Entity(enemy1.Index, enemy1.Version);
        Console.WriteLine($"已销毁的实体仍然有效: {entityManager.IsValid(fakeEnemy)}"); // False

        // 在相同索引位置创建新实体（版本号会不同）
        Entity newEntity = entityManager.Create();
        Console.WriteLine($"新实体: {newEntity}"); // 版本号会不同

        // 获取统计信息
        var stats = entityManager.GetStats();
        Console.WriteLine($"统计: 活跃={stats.ActiveEntityCount}, " +
                          $"回收={stats.FreeIndexCount}, " +
                          $"下一个索引={stats.NextIndex}");
    }
}