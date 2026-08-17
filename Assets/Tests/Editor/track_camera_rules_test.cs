using NUnit.Framework;
using UnityEngine;

public class TrackCameraRulesTests
{
    private static readonly Vector3[] ClosedTrack =
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(1f, 0f, 0f),
        new Vector3(2f, 0f, 0f),
        new Vector3(3f, 0f, 0f),
        new Vector3(4f, 0f, 0f),
        new Vector3(5f, 0f, 0f),
        new Vector3(6f, 0f, 0f),
        new Vector3(7f, 0f, 0f)
    };

    [TestCase(-1, 8, 7)]
    [TestCase(8, 8, 0)]
    [TestCase(17, 8, 1)]
    public void test_wrap_index_outside_track_returns_wrapped_index(
        int index,
        int count,
        int expected)
    {
        Assert.That(RaceCameraRules.WrapIndex(index, count), Is.EqualTo(expected));
    }

    [Test]
    public void test_find_closest_position_uses_nearest_track_node()
    {
        int result = RaceCameraRules.FindClosestPositionIndex(
            ClosedTrack,
            new Vector3(5.2f, 0f, 0f));

        Assert.That(result, Is.EqualTo(5));
    }

    [Test]
    public void test_window_bounds_at_start_wraps_behind_finish_line()
    {
        Bounds bounds = RaceCameraRules.CalculateWindowBounds(
            ClosedTrack,
            0,
            2,
            1);

        Assert.That(bounds.min.x, Is.EqualTo(0f));
        Assert.That(bounds.max.x, Is.EqualTo(7f));
    }

    [Test]
    public void test_orthographic_size_fits_widest_axis_and_padding()
    {
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(16f, 4f, 0f));

        float result = RaceCameraRules.CalculateOrthographicSize(
            bounds,
            2f,
            1.25f,
            0.5f);

        Assert.That(result, Is.EqualTo(5f).Within(0.001f));
    }

    [Test]
    public void test_orthographic_size_honors_minimum_for_single_node()
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        float result = RaceCameraRules.CalculateOrthographicSize(
            bounds,
            16f / 9f,
            1.15f,
            0.75f);

        Assert.That(result, Is.EqualTo(0.75f));
    }

    [Test]
    public void test_drag_world_offset_moves_camera_opposite_pointer()
    {
        Vector3 result = RaceCameraRules.CalculateDragWorldOffset(
            new Vector2(100f, -50f),
            5f,
            1000f,
            1f);

        Assert.That(result.x, Is.EqualTo(-1f).Within(0.001f));
        Assert.That(result.y, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void test_scroll_zoom_is_exponential_and_clamped()
    {
        float zoomedIn = RaceCameraRules.CalculateScrolledOrthographicSize(
            10f, 1f, 0.2f, 2f, 12f);
        float clampedOut = RaceCameraRules.CalculateScrolledOrthographicSize(
            10f, -100f, 0.2f, 2f, 12f);

        Assert.That(zoomedIn, Is.LessThan(10f));
        Assert.That(clampedOut, Is.EqualTo(12f));
    }

    [Test]
    public void test_manual_focus_override_resets_only_at_next_turn()
    {
        var state = new RaceCameraFocusState();

        state.BeginTurn();
        Assert.IsTrue(state.AllowsAutomaticFocus);

        state.TakeManualControl();
        Assert.IsFalse(state.AllowsAutomaticFocus);
        Assert.IsTrue(state.ManualOverrideThisTurn);

        state.BeginTurn();
        Assert.IsTrue(state.AllowsAutomaticFocus);
        Assert.IsFalse(state.ManualOverrideThisTurn);
    }
}
