using System.Buffers;
using System.Drawing;
using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace PixelAutomation.Tool.Overlay.WPF.Services;

/// <summary>
/// Central manager for all object pools in the application
/// Provides high-performance object reuse for frequently allocated objects
/// </summary>
public static class ObjectPoolManager
{
    // Array pools for high-frequency coordinate and color operations
    public static readonly ArrayPool<Point> PointArrayPool = ArrayPool<Point>.Create(maxArrayLength: 64, maxArraysPerBucket: 16);
    public static readonly ArrayPool<Color> ColorArrayPool = ArrayPool<Color>.Create(maxArrayLength: 64, maxArraysPerBucket: 16);
    public static readonly ArrayPool<int> IntArrayPool = ArrayPool<int>.Create(maxArrayLength: 256, maxArraysPerBucket: 8);
    
    
    // Object pools for collections
    private static readonly DefaultObjectPool<List<Point>> _pointListPool;
    private static readonly DefaultObjectPool<List<Color>> _colorListPool;
    private static readonly DefaultObjectPool<List<IntPtr>> _intPtrListPool;
    private static readonly DefaultObjectPool<Dictionary<string, object>> _stringObjectDictPool;
    private static readonly DefaultObjectPool<Dictionary<Point, Color>> _pointColorDictPool;
    private static readonly DefaultObjectPool<StringBuilder> _stringBuilderPool;
    
    // ViewModel-specific pools for high-frequency UI operations
    private static readonly DefaultObjectPool<List<object>> _clientViewModelListPool;
    private static readonly DefaultObjectPool<List<object>> _attackSkillViewModelListPool;
    
    static ObjectPoolManager()
    {
        
        // Initialize collection pools with clearing policies
        _pointListPool = new DefaultObjectPool<List<Point>>(new ClearingListPolicy<Point>(), maximumRetained: 32);
        _colorListPool = new DefaultObjectPool<List<Color>>(new ClearingListPolicy<Color>(), maximumRetained: 16);
        _intPtrListPool = new DefaultObjectPool<List<IntPtr>>(new ClearingListPolicy<IntPtr>(), maximumRetained: 8);
        _stringObjectDictPool = new DefaultObjectPool<Dictionary<string, object>>(new ClearingDictionaryPolicy<string, object>(), maximumRetained: 16);
        _pointColorDictPool = new DefaultObjectPool<Dictionary<Point, Color>>(new ClearingDictionaryPolicy<Point, Color>(), maximumRetained: 8);
        _stringBuilderPool = new DefaultObjectPool<StringBuilder>(new StringBuilderPooledObjectPolicy(), maximumRetained: 16);
        
        // Initialize ViewModel pools for UI operations
        _clientViewModelListPool = new DefaultObjectPool<List<object>>(new ClearingListPolicy<object>(), maximumRetained: 16);
        _attackSkillViewModelListPool = new DefaultObjectPool<List<object>>(new ClearingListPolicy<object>(), maximumRetained: 8);
    }
    
    
    // Point array operations
    public static Point[] RentPointArray(int minimumLength) => PointArrayPool.Rent(minimumLength);
    public static void ReturnPointArray(Point[] array, bool clearArray = false)
    {
        PointArrayPool.Return(array, clearArray);
    }
    
    // Color array operations
    public static Color[] RentColorArray(int minimumLength) => ColorArrayPool.Rent(minimumLength);
    public static void ReturnColorArray(Color[] array, bool clearArray = false)
    {
        ColorArrayPool.Return(array, clearArray);
    }
    
    // Int array operations
    public static int[] RentIntArray(int minimumLength) => IntArrayPool.Rent(minimumLength);
    public static void ReturnIntArray(int[] array, bool clearArray = false)
    {
        IntArrayPool.Return(array, clearArray);
    }
    
    // Collection pools
    public static List<Point> GetPointList() => _pointListPool.Get();
    public static void ReturnPointList(List<Point> list) => _pointListPool.Return(list);
    
    public static List<Color> GetColorList() => _colorListPool.Get();
    public static void ReturnColorList(List<Color> list) => _colorListPool.Return(list);
    
    public static List<IntPtr> GetIntPtrList() => _intPtrListPool.Get();
    public static void ReturnIntPtrList(List<IntPtr> list) => _intPtrListPool.Return(list);
    
    public static Dictionary<string, object> GetStringObjectDict() => _stringObjectDictPool.Get();
    public static void ReturnStringObjectDict(Dictionary<string, object> dict) => _stringObjectDictPool.Return(dict);
    
    public static Dictionary<Point, Color> GetPointColorDict() => _pointColorDictPool.Get();
    public static void ReturnPointColorDict(Dictionary<Point, Color> dict) => _pointColorDictPool.Return(dict);
    
    public static StringBuilder GetStringBuilder() => _stringBuilderPool.Get();
    public static void ReturnStringBuilder(StringBuilder sb) => _stringBuilderPool.Return(sb);
    
    // ViewModel-specific pools (using object lists for type flexibility)
    public static List<T> GetClientViewModelList<T>() => (List<T>)(object)_clientViewModelListPool.Get();
    public static void ReturnClientViewModelList<T>(List<T> list) => _clientViewModelListPool.Return((List<object>)(object)list);
    
    public static List<T> GetAttackSkillViewModelList<T>() => (List<T>)(object)_attackSkillViewModelListPool.Get();
    public static void ReturnAttackSkillViewModelList<T>(List<T> list) => _attackSkillViewModelListPool.Return((List<object>)(object)list);
}

/// <summary>
/// Policy for List<T> objects that clears the list before returning to pool
/// </summary>
public class ClearingListPolicy<T> : PooledObjectPolicy<List<T>>
{
    public override List<T> Create() => new List<T>();
    
    public override bool Return(List<T> obj)
    {
        if (obj == null) return false;
        
        // Clear the list but keep capacity for reuse
        obj.Clear();
        
        // Don't pool extremely large lists to avoid memory bloat
        return obj.Capacity <= 1024;
    }
}

/// <summary>
/// Policy for Dictionary<TKey, TValue> objects that clears the dictionary before returning to pool
/// </summary>
public class ClearingDictionaryPolicy<TKey, TValue> : PooledObjectPolicy<Dictionary<TKey, TValue>>
    where TKey : notnull
{
    public override Dictionary<TKey, TValue> Create() => new Dictionary<TKey, TValue>();
    
    public override bool Return(Dictionary<TKey, TValue> obj)
    {
        if (obj == null) return false;
        
        // Clear the dictionary but keep capacity for reuse
        obj.Clear();
        
        // Don't pool extremely large dictionaries to avoid memory bloat
        return obj.Count <= 256;
    }
}

/// <summary>
/// Factory for pooled Point objects
/// Since Point is a struct, this provides a reusable factory pattern
/// </summary>
public static class PooledPoint
{
    /// <summary>
    /// Creates a Point with specified coordinates using object pooling
    /// Note: Since Point is a value type, this actually just returns a new Point
    /// The benefit is in standardizing the API and potential future optimizations
    /// </summary>
    public static Point Create(int x, int y) => new Point(x, y);
    
    /// <summary>
    /// For value types like Point, return is a no-op, but maintains API consistency
    /// </summary>
    public static void Return(Point point) 
    {
        // No-op for value types, but maintains consistent API
    }
}