using Godot;

namespace Blitz.Scripts;

public class Utils
{
    /// <summary>
    /// Returns a vector2 around the origin
    /// </summary>
    /// <returns></returns>
    public static Vector2 AroundVec2(Vector2 origin, float maxDelta)
    {
        
        var rng= new RandomNumberGenerator();
        var f1= rng.RandfRange(-maxDelta, maxDelta);
        var f2= rng.RandfRange(-maxDelta, maxDelta);
        return new Vector2(origin.X + f1, origin.Y + f2);
    }
}