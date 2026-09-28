/// <summary>Actual cards removed by cooling, grouped by their source before mutation.</summary>
public readonly struct HeatCoolingResult
{
    public HeatCoolingResult(int fromHand, int fromDraw, int fromDiscard)
    {
        FromHand = fromHand;
        FromDraw = fromDraw;
        FromDiscard = fromDiscard;
    }

    public int FromHand { get; }
    public int FromDraw { get; }
    public int FromDiscard { get; }
    /// <summary>Includes destroyed temporary heat, matching the existing cooling count contract.</summary>
    public int Total => FromHand + FromDraw + FromDiscard;
}
