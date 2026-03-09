namespace Content.Shared._RD;

public abstract class RDEntitySystemSingleton<TComponent> : RDEntitySystem where TComponent : IComponent, new()
{
    [ViewVariables]
    protected Entity<TComponent> Inst => GetInst();

    protected Entity<TComponent> GetInst()
    {
        var query = EntityQueryEnumerator<TComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            return (uid, component);
        }

        var instance = Spawn();
        var entity = (instance, AddComp<TComponent>(instance));
        OnInstanceCreated(entity);
        return entity;
    }

    protected virtual void OnInstanceCreated(Entity<TComponent> entity)
    {
    }

    protected void Dirty()
    {
        Dirty(Inst);
    }

    protected void DirtyField(string field)
    {
        if (Inst.Comp is not IComponentDelta delta)
        {
            Log.Fatal("You dumb, yor singleton component not fieldDeltas, what are you doing????!");
            return;
        }

        DirtyField(Inst, delta, field);
    }
}
