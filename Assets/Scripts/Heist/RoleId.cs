namespace Banda.Core
{
    /// <summary>
    /// Ідентифікатори ролей банди (docs/TZ.md, розділ «Ролі»).
    /// Лежить у збірці Banda.Heist (без залежностей від рушія), бо ролі потрібні симуляції справи.
    /// </summary>
    public enum RoleId
    {
        None = -1,
        Hacker = 0,
        Shadow = 1,
        Safecracker = 2,
        Distractor = 3,
        Driver = 4,
    }
}
