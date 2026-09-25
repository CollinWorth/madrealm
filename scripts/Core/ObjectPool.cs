using Godot;
using System.Collections.Generic;

namespace MadRealm.Core;

// Autoload singleton (registered as "BulletPool" in project settings).
// Bullet-hell games spawn/destroy hundreds of projectiles per second;
// instantiating and freeing a Node for every single one causes constant
// allocation churn and GC pressure that shows up as stutter once bullet
// counts get high. This keeps a per-scene free list and recycles
// instances instead. Not bullet-specific -- anything spawned in bursts
// (hit sparks, pickups) can use the same pool later.
public partial class ObjectPool : Node
{
    public static ObjectPool Instance { get; private set; }

    private readonly Dictionary<PackedScene, Queue<Node2D>> _pools = new();
    private readonly Dictionary<Node2D, PackedScene> _originScene = new();

    public override void _Ready()
    {
        Instance = this;
    }

    public T Get<T>(PackedScene scene) where T : Node2D
    {
        if (!_pools.TryGetValue(scene, out var queue))
        {
            queue = new Queue<Node2D>();
            _pools[scene] = queue;
        }

        Node2D instance;
        if (queue.Count > 0)
        {
            instance = queue.Dequeue();
            instance.Visible = true;
            instance.SetProcess(true);
            instance.SetPhysicsProcess(true);
            instance.ProcessMode = ProcessModeEnum.Inherit;
        }
        else
        {
            instance = (Node2D)scene.Instantiate();
            _originScene[instance] = scene;
            AddChild(instance);
        }

        return (T)instance;
    }

    // Called by the object itself (e.g. Bullet on hit/expiry) instead of
    // QueueFree(). Anything not spawned through Get() is just freed,
    // since there's no pool entry to return it to.
    public void Release(Node2D instance)
    {
        if (!_originScene.TryGetValue(instance, out var scene))
        {
            instance.QueueFree();
            return;
        }

        instance.Visible = false;
        instance.SetProcess(false);
        instance.SetPhysicsProcess(false);
        instance.ProcessMode = ProcessModeEnum.Disabled;
        _pools[scene].Enqueue(instance);
    }
}
