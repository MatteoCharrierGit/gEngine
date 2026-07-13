namespace gEngine.Ecs.Base;

public static class WorldQueries
{
    public static IEnumerable<(Entity Entity, T Component)> Query<T>(this World world)
    {
        var storage = world.GetStorage<T>();

        if (storage == null)
            yield break;

        foreach (var pair in storage.Items)
        {
            int entityId = pair.Key;
            T component = pair.Value;

            yield return (world.Entities[entityId], component);
        }
    }
    
    
    public static IEnumerable<(Entity Entity, T1 C1, T2 C2)> Query<T1, T2>(this World world)
    {
        var s1 = world.GetStorage<T1>();
        var s2 = world.GetStorage<T2>();

        if (s1 == null || s2 == null)
            yield break;

        if (s1.Count <= s2.Count)
        {
            foreach (var (id, c1) in s1.Items)
            {
                if (!s2.TryGet(id, out var c2))
                    continue;

                yield return (world.Entities[id], c1, c2);
            }
        }
        else
        {
            foreach (var (id, c2) in s2.Items)
            {
                if (!s1.TryGet(id, out var c1))
                    continue;

                yield return (world.Entities[id], c1, c2);
            }
        }
    }
    
    public static IEnumerable<(Entity Entity, T1 C1, T2 C2, T3 C3)> Query<T1, T2, T3>(this World world)
    {
        var s1 = world.GetStorage<T1>();
        var s2 = world.GetStorage<T2>();
        var s3 = world.GetStorage<T3>();

        if (s1 == null || s2 == null || s3 == null)
            yield break;

        var smallest = Math.Min(s1.Count, Math.Min(s2.Count, s3.Count));

        if (smallest == s1.Count)
        {
            foreach (var pair in s1.Items)
            {
                if (!s2.TryGet(pair.Key, out var c2) ||
                    !s3.TryGet(pair.Key, out var c3))
                    continue;

                yield return (world.Entities[pair.Key], pair.Value, c2, c3);
            }
        }
        else if (smallest == s2.Count)
        {
            foreach (var pair in s2.Items)
            {
                if (!s1.TryGet(pair.Key, out var c1) ||
                    !s3.TryGet(pair.Key, out var c3))
                    continue;

                yield return (world.Entities[pair.Key], c1, pair.Value, c3);
            }
        }
        else
        {
            foreach (var pair in s3.Items)
            {
                if (!s1.TryGet(pair.Key, out var c1) ||
                    !s2.TryGet(pair.Key, out var c2))
                    continue;

                yield return (world.Entities[pair.Key], c1, c2, pair.Value);
            }
        }
    }
    
    public static IEnumerable<(Entity Entity, T1 C1, T2 C2, T3 C3, T4 C4)> Query<T1, T2, T3, T4>(this World world)
    {
        var s1 = world.GetStorage<T1>();
        var s2 = world.GetStorage<T2>();
        var s3 = world.GetStorage<T3>();
        var s4 = world.GetStorage<T4>();

        if (s1 == null || s2 == null || s3 == null || s4 == null)
            yield break;

        var smallest = Math.Min(Math.Min(s1.Count, s2.Count), Math.Min(s3.Count, s4.Count));

        if (smallest == s1.Count)
        {
            foreach (var pair in s1.Items)
            {
                if (!s2.TryGet(pair.Key, out var c2) ||
                    !s3.TryGet(pair.Key, out var c3) ||
                    !s4.TryGet(pair.Key, out var c4))
                    continue;

                yield return (world.Entities[pair.Key], pair.Value, c2, c3, c4);
            }
        }
        else if (smallest == s2.Count)
        {
            foreach (var pair in s2.Items)
            {
                if (!s1.TryGet(pair.Key, out var c1) ||
                    !s3.TryGet(pair.Key, out var c3) ||
                    !s4.TryGet(pair.Key, out var c4))
                    continue;

                yield return (world.Entities[pair.Key], c1, pair.Value, c3, c4);
            }
        }
        else if (smallest == s3.Count)
        {
            foreach (var pair in s3.Items)
            {
                if (!s1.TryGet(pair.Key, out var c1) ||
                    !s2.TryGet(pair.Key, out var c2) ||
                    !s4.TryGet(pair.Key, out var c4))
                    continue;

                yield return (world.Entities[pair.Key], c1, c2, pair.Value, c4);
            }
        }
        else
        {
            foreach (var pair in s4.Items)
            {
                if (!s1.TryGet(pair.Key, out var c1) ||
                    !s2.TryGet(pair.Key, out var c2) ||
                    !s3.TryGet(pair.Key, out var c3))
                    continue;

                yield return (world.Entities[pair.Key], c1, c2, c3, pair.Value);
            }
        }
    }
}