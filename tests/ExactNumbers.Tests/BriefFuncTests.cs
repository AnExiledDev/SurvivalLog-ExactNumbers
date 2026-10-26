using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

/// <summary>Which furniture function's preview the crafting table shows for a furniture product, picked as the game picks it.</summary>
public class BriefFuncTests
{
    private static FurnitureFunc? Func(int id, int weight, string? preview) => new FurnitureFunc(id, weight, preview);

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Furniture_of_the_slot_types_the_game_shows_no_brief_for_has_no_function(int slotType)
    {
        Assert.Null(BriefFunc.Choose(slotType, new[] { Func(1, 5, "Satiety:+10") }));
    }

    [Fact]
    public void The_function_with_the_highest_recommend_weight_is_shown_not_simply_the_first()
    {
        var chosen = BriefFunc.Choose(1, new[] { Func(1, 5, "Satiety:+10"), Func(2, 9, "Morale:+3"), Func(3, 7, "Stamina:+20") });

        Assert.Equal(2, chosen!.Value.Id);
    }

    [Fact]
    public void On_a_tie_the_first_function_listed_is_shown()
    {
        var chosen = BriefFunc.Choose(1, new[] { Func(1, 5, "Satiety:+10"), Func(2, 5, "Morale:+3") });

        Assert.Equal(1, chosen!.Value.Id);
    }

    [Fact]
    public void Only_a_function_whose_preview_holds_a_gain_is_shown()
    {
        Assert.Equal(2, BriefFunc.Choose(1, new[] { Func(1, 9, "Satiety:-10"), Func(2, 1, "Satiety:-5;Morale:+3") })!.Value.Id);
        Assert.Null(BriefFunc.Choose(1, new[] { Func(1, 9, "Satiety:-10"), Func(2, 9, "") }));
    }

    [Fact]
    public void A_function_missing_from_the_configs_is_skipped()
    {
        Assert.Equal(2, BriefFunc.Choose(1, new[] { null, Func(2, 1, "Morale:+3"), Func(3, 4, null) })!.Value.Id);
    }

    // ===== Choices kept between recipe lists =====

    private static BriefChoice Choice(int funcId) => new(true, 1, new FurnitureFunc(funcId, 5, "Satiety:+10"));

    /// <summary>A recipe list asks for the same furniture again and again; the choice is read from the configs once.</summary>
    [Fact]
    public void A_choice_is_kept_for_the_config_table_it_was_read_from()
    {
        var choices = new BriefChoices();

        Assert.False(choices.TryGet(100, 500, out _));

        choices.Put(500, Choice(7));

        Assert.True(choices.TryGet(100, 500, out var kept));
        Assert.Equal(7, kept.Func!.Value.Id);
        Assert.False(choices.TryGet(100, 501, out _));
    }

    /// <summary>Loading a game can load the configs again; a choice read from the old table must not outlive it.</summary>
    [Fact]
    public void A_new_config_table_drops_every_choice_read_from_the_old_one()
    {
        var choices = new BriefChoices();

        choices.TryGet(100, 500, out _);
        choices.Put(500, Choice(7));

        Assert.False(choices.TryGet(200, 500, out _));

        choices.Put(500, Choice(8));

        Assert.True(choices.TryGet(200, 500, out var kept));
        Assert.Equal(8, kept.Func!.Value.Id);
        Assert.False(choices.TryGet(100, 500, out _));
    }

    [Fact]
    public void Past_the_cap_a_choice_is_not_kept()
    {
        var choices = new BriefChoices();

        choices.TryGet(100, 0, out _);

        for (var id = 1; id <= BriefChoices.MaxKept; id++) choices.Put(id, Choice(7));

        choices.Put(BriefChoices.MaxKept + 1, Choice(7));

        Assert.True(choices.TryGet(100, BriefChoices.MaxKept, out _));
        Assert.False(choices.TryGet(100, BriefChoices.MaxKept + 1, out _));
    }
}
