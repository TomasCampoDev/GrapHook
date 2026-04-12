/// <summary>
/// Utilidades matemáticas compartidas entre módulos.
/// </summary>
public static class MathUtility
{
    /// Devuelve el índice envuelto dentro del rango [0, count).
    /// Funciona correctamente con valores negativos.
    public static int WrapIndex(int index, int count)
    {
        return ((index % count) + count) % count;
    }
}
