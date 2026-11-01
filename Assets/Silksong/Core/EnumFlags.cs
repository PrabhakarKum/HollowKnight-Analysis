using System;

namespace Hollowknight.Silksong
{
    public static class EnumFlags
    {
        // Enum.HasFlag reads as true for a zero flag; this compares the raw bits and treats
        // a zero flag as "not set" so callers cannot get a false positive.
        public static bool Has<T>(this T value, T flag) where T : Enum
        {
            long v = Convert.ToInt64(value);
            long f = Convert.ToInt64(flag);
            return f != 0 && (v & f) == f;
        }
    }
}
