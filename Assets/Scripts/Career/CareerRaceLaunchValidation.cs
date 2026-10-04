using System;

/// <summary>
/// Read-only career launch gate. The scene coordinator owns the actual save load
/// and track lookup; this rule preserves their failure precedence and copy.
/// </summary>
public static class CareerRaceLaunchValidation
{
    public static bool TryValidate(
        CareerRaceLaunchRequest request,
        CareerLoadResult loaded,
        string loadedTrackId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (loaded.Status != CareerLoadStatus.Loaded)
        {
            failureReason = loaded.Status == CareerLoadStatus.Invalid
                ? "生涯存档校验失败"
                : "未找到当前生涯存档";
            return false;
        }

        if (!request.Matches(loaded.State))
        {
            failureReason = "启动请求与当前生涯存档不一致";
            return false;
        }

        if (!string.Equals(loadedTrackId, request.TrackId, StringComparison.Ordinal))
        {
            failureReason = "指定正式赛道加载失败，已阻止 fallback 赛道计入生涯";
            return false;
        }
        return true;
    }
}
