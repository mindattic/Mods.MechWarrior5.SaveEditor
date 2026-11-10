namespace MechWarrior5.Clans.SaveEditor.Core.Game;

/// <summary>
/// The mech lab's plans (<c>MechPlanningModel.PlanDetails</c> in <c>PersistentModelData</c>): one
/// <c>KelMechPlanRecord {MechInventoryId, PlannedMechInventoryId}</c> per owned mech, pairing it with its planned
/// copy, a mech actor tagged <c>Inventory.Planned</c> in the inventory. Saves without the model have no plans.
/// </summary>
internal sealed class MechPlans
{
    public const string PlannedTag = "Inventory.Planned";

    private readonly StructArrayStorage? _records;

    internal MechPlans(PropertyList models)
    {
        _records = models.Find<ObjectProperty>("MechPlanningModel")?.Inline?.Find<ArrayProperty>("PlanDetails")?.Storage as StructArrayStorage;
    }

    /// <summary>The plan record of a mech; null when it has none.</summary>
    public PropertyList? RecordOf(Guid mechId) =>
        _records?.PropertyElements.FirstOrDefault(r => Id(r, "MechInventoryId") == mechId);

    public static Guid? PlannedId(PropertyList record) => Id(record, "PlannedMechInventoryId");

    /// <summary>Takes a record out, returning where it was so <see cref="Restore"/> can put it back.</summary>
    public int Detach(PropertyList record)
    {
        var at = _records?.Elements.IndexOf(record) ?? -1;
        if (at >= 0)
        {
            _records!.Elements.RemoveAt(at);
        }

        return at;
    }

    public void Restore(int at, PropertyList record)
    {
        if (at >= 0)
        {
            _records?.Elements.Insert(at, record);
        }
    }

    /// <summary>Adds a record for a new mech, modeled on <paramref name="template"/>.</summary>
    public void Add(PropertyList template, Guid mechId, Guid plannedId)
    {
        var record = template.DeepClone();
        SetId(record, "MechInventoryId", mechId);
        SetId(record, "PlannedMechInventoryId", plannedId);
        _records?.Elements.Add(record);
    }

    private static Guid? Id(PropertyList record, string name) => Ue.ItemGuid(record.Find<StructProperty>(name)?.Value as PropertyList);

    private static void SetId(PropertyList record, string name, Guid id) =>
        ((PropertyList)record.Get<StructProperty>(name).Value).Get<StructProperty>("Value").Value = new GuidValue(id);
}
