namespace MechWarrior5.Clans.SaveEditor.Core.Game;

/// <summary>A skipped item and why, for batch reports.</summary>
public sealed record Skipped(string Item, string Reason);

/// <summary>What a batch operation did.</summary>
public sealed record BatchResult(IReadOnlyList<string> Succeeded, IReadOnlyList<Skipped> Skipped);

/// <summary>
/// Everything the player owns (<c>InventoryData</c>): currencies, mech XP, salvage, crew,
/// weapons and equipment as stacks; mechs; pilots; unlocks.
/// </summary>
public sealed class InventoryEditor
{
    /// <summary>The game caps stacks at int32; keep well clear so arithmetic in the game can't overflow.</summary>
    public const int MaxQuantity = 1_000_000_000;

    private readonly StructArrayStorage _array;
    private readonly Func<Guid, bool> _isReferenced;
    private readonly List<InventoryActor> _actors;

    internal InventoryEditor(StructArrayStorage array, Func<Guid, bool> isReferenced)
    {
        _array = array;
        _isReferenced = isReferenced;
        _actors = [.. array.PropertyElements.Select(e => new InventoryActor(e))];
    }

    public IReadOnlyList<InventoryActor> Actors => _actors;

    /// <summary>The player's item stacks (not the shop's stock, which shares the array).</summary>
    public IEnumerable<InventoryActor> Stacks => AllStacks.Where(a => a.IsPlayers);

    /// <summary>Every item stack in the save, the shop's included: what items the campaign knows about.</summary>
    public IEnumerable<InventoryActor> AllStacks => _actors.Where(a => a.Kind == ActorKind.Stackable);

    /// <summary>The player's mechs (not shop, planned, saved-loadout or unavailable ones).</summary>
    public IEnumerable<ClansMech> Mechs => _actors.Where(a => a.Kind == ActorKind.Mech && a.IsPlayers).Select(a => new ClansMech(a));

    public IEnumerable<InventoryActor> Pilots => _actors.Where(a => a.Kind == ActorKind.Pilot);

    public IEnumerable<InventoryActor> Unlocks => _actors.Where(a => a.Kind == ActorKind.Unlock);

    /// <summary>Total held of one asset across all its stacks (equipment can be split by condition).</summary>
    public int Total(string assetName) => Stacks.Where(s => s.AssetName == assetName).Sum(s => s.Quantity);

    /// <summary>Sets the total held of an asset: the first stack takes the difference, extra stacks are kept.</summary>
    public void SetTotal(string assetType, string assetName, int total)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(total, MaxQuantity);
        var stacks = Stacks.Where(s => s.AssetName == assetName).ToList();
        if (stacks.Count == 0)
        {
            if (total > 0)
            {
                AddStack(assetType, assetName, total);
            }

            return;
        }

        var others = stacks.Skip(1).Sum(s => s.Quantity);
        if (total >= others)
        {
            stacks[0].Quantity = total - others;
            return;
        }

        // Lowering below the extra stacks: drain them in order.
        var remaining = total;
        foreach (var stack in stacks)
        {
            var keep = Math.Min(stack.Quantity, remaining);
            stack.Quantity = keep;
            remaining -= keep;
        }
    }

    /// <summary>Adds <paramref name="quantity"/> of an asset, topping up an existing stack or creating one.</summary>
    public void AddStack(string assetType, string assetName, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        if (Stacks.FirstOrDefault(s => s.AssetName == assetName) is { } existing)
        {
            existing.Quantity = (int)Math.Min(MaxQuantity, (long)existing.Quantity + quantity);
            return;
        }

        // Cloned from one of the player's stacks so the new stack carries the player's inventory tag.
        var template = Stacks.FirstOrDefault(s => s.AssetType == assetType) ?? Stacks.FirstOrDefault()
            ?? throw new InvalidOperationException("This save has no item stack to model a new one on.");
        var stack = template.CloneWithNewId();
        if (stack.ItemData?.Find<StructProperty>("AssetId")?.Value is PropertyList assetId
            && (assetId.Find<StructProperty>("ID")?.Value as PropertyList) is { } id)
        {
            id.Get<NameProperty>("PrimaryAssetName").Value = assetName;
            if (id.Find<StructProperty>("PrimaryAssetType")?.Value is PropertyList type)
            {
                type.Get<NameProperty>("Name").Value = assetType;
            }
        }

        stack.Quantity = quantity;
        Append(stack);
    }

    /// <summary>Removes item stacks outright (currencies are set to zero instead, never removed).</summary>
    public BatchResult RemoveStacks(IEnumerable<InventoryActor> stacks)
    {
        var done = new List<string>();
        var skipped = new List<Skipped>();
        foreach (var stack in stacks.Where(s => s.Kind == ActorKind.Stackable).ToList())
        {
            if (stack.Category is StackCategory.Currency or StackCategory.Salvage or StackCategory.MechXp or StackCategory.Crew)
            {
                stack.Quantity = 0;
                skipped.Add(new Skipped(stack.AssetName, "set to zero; the campaign expects this entry to exist"));
                continue;
            }

            Remove(stack);
            done.Add(stack.AssetName);
        }

        return new BatchResult(done, skipped);
    }

    /// <summary>Adds copies of mechs (same loadout, new inventory ids); they arrive repaired.</summary>
    public BatchResult DuplicateMechs(IEnumerable<ClansMech> mechs, int copies = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(copies, 1);
        var done = new List<string>();
        foreach (var mech in mechs.ToList())
        {
            for (var i = 0; i < copies; i++)
            {
                var clone = new ClansMech(mech.Actor.CloneWithNewId());
                clone.Repair(FullStructure(mech));
                Append(clone.Actor);
            }

            done.Add(Label(mech));
        }

        return new BatchResult(done, []);
    }

    /// <summary>Removes mechs that nothing else in the campaign refers to (deployment, technicians, plans).</summary>
    public BatchResult RemoveMechs(IEnumerable<ClansMech> mechs)
    {
        var done = new List<string>();
        var skipped = new List<Skipped>();
        foreach (var mech in mechs.ToList())
        {
            if (mech.InventoryId is not { } id)
            {
                skipped.Add(new Skipped(Label(mech), "it has no inventory id"));
            }
            else if (_isReferenced(id))
            {
                skipped.Add(new Skipped(Label(mech), "it is deployed, being worked on, or planned"));
            }
            else
            {
                Remove(mech.Actor);
                done.Add(Label(mech));
            }
        }

        return new BatchResult(done, skipped);
    }

    /// <summary>
    /// A variant's full internal structure, taken from an undamaged entry of it in this save (the shop's, planned or
    /// unavailable copies included), else of its chassis; null when the save has none. The save keeps no maximum.
    /// </summary>
    public IReadOnlyDictionary<string, float>? FullStructure(ClansMech mech)
    {
        var pristine = _actors.Where(a => a.Kind == ActorKind.Mech).Select(a => new ClansMech(a)).Where(m => !m.IsDamaged).ToList();
        var references = pristine.Where(m => m.Variant == mech.Variant).ToList() is { Count: > 0 } same
            ? same
            : pristine.Where(m => m.Chassis == mech.Chassis).ToList();
        return references.Count == 0
            ? null
            : references.SelectMany(m => m.Structure).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Max(kv => kv.Value));
    }

    public BatchResult RepairMechs(IEnumerable<ClansMech> mechs)
    {
        var done = new List<string>();
        var skipped = new List<Skipped>();
        foreach (var mech in mechs.ToList())
        {
            if (mech.Repair(FullStructure(mech)))
            {
                done.Add(Label(mech));
            }
            else
            {
                skipped.Add(new Skipped(Label(mech), "already in full repair"));
            }
        }

        return new BatchResult(done, skipped);
    }

    internal void Commit()
    {
        foreach (var actor in _actors)
        {
            actor.Commit();
        }
    }

    private static string Label(ClansMech mech) =>
        string.IsNullOrEmpty(mech.CustomName) ? mech.Variant : $"{mech.CustomName} ({mech.Variant})";

    private void Append(InventoryActor actor)
    {
        _actors.Add(actor);
        _array.Elements.Add(actor.Element);
    }

    private void Remove(InventoryActor actor)
    {
        _actors.Remove(actor);
        _array.Elements.Remove(actor.Element);
    }
}
