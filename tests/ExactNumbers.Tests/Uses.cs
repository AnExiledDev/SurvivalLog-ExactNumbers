using ExactNumbers.Core;

namespace ExactNumbers.Tests;

/// <summary>Cost calls, uses and samples shaped the way the hooks and the tracker make them, for the measuring tests.</summary>
internal static class Uses
{
    public const int Furniture = 500;
    public const int Func = 7;
    public const int ActionId = 11;

    public static readonly MeasureSite Bed = new(Furniture, Func);

    /// <summary>
    /// One CostAttrValue call on the character. By default an action's own change, sourced to the bed, which the stat
    /// took in full: after is before plus value unless given.
    /// </summary>
    public static CostCall Call(
        float value,
        float before = 50,
        float? after = null,
        Stat? stat = Stat.Stamina,
        bool isAction = true,
        bool isFurniture = true,
        int configId = Furniture,
        bool isDependency = false,
        bool hasChange = true,
        float max = 100,
        int funcId = 0,
        int at = 0)
    {
        var change = hasChange ? (StatChange?)new StatChange(value, before, after ?? before + value) : null;

        return new CostCall(
            stat?.ToString() ?? "Hp",
            stat,
            value,
            1,
            isAction ? "Action" : "Normal",
            isAction,
            isDependency,
            isFurniture ? "Furniture" : "None",
            isFurniture,
            configId,
            change,
            max,
            ActionId,
            funcId,
            at);
    }

    public static UseStart Use(bool isIndefinite = false, int funcId = Func, int actionId = ActionId) => new(actionId, 1, funcId, isIndefinite, 0);

    public static UseSample Sample(double amount, double reach = 0, bool endedAtMax = false, bool isRate = false, MeasureSite? site = null, Stat stat = Stat.Stamina) =>
        new(new MeasureKey(site ?? Bed, stat), isRate, amount, reach, endedAtMax);

    public static MeasureStore Store(params UseSample[] samples)
    {
        var store = new MeasureStore();

        foreach (var sample in samples) store.Add(sample);

        return store;
    }
}
