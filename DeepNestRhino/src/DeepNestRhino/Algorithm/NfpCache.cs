using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DeepNestRhino.Geometry;

namespace DeepNestRhino.Algorithm
{
    /// <summary>
    /// Thread-safe cache for computed No-Fit Polygons.
    /// Maps the window.nfpcache / db object from background.js (lines 45-97).
    /// Cache key format: "A{source}B{source}Arot{rotation}Brot{rotation}"
    /// </summary>
    public class NfpCache
    {
        private readonly ConcurrentDictionary<string, CachedNfp> _cache = new();

        public string MakeKey(int aSource, int bSource, double aRotation, double bRotation)
        {
            return $"A{aSource}B{bSource}Arot{(int)aRotation}Brot{(int)bRotation}";
        }

        public bool Has(int aSource, int bSource, double aRotation, double bRotation)
        {
            return _cache.ContainsKey(MakeKey(aSource, bSource, aRotation, bRotation));
        }

        /// <summary>
        /// Try to find a cached outer NFP.
        /// </summary>
        public NestPolygon FindOuter(int aSource, int bSource, double aRotation, double bRotation)
        {
            var key = MakeKey(aSource, bSource, aRotation, bRotation);
            if (_cache.TryGetValue(key, out var cached))
                return cached.Nfp?.DeepClone();
            return null;
        }

        /// <summary>
        /// Try to find a cached inner NFP (returns list of NFPs).
        /// </summary>
        public List<NestPolygon> FindInner(int aSource, int bSource, double aRotation, double bRotation)
        {
            var key = MakeKey(aSource, bSource, aRotation, bRotation);
            if (_cache.TryGetValue(key, out var cached) && cached.InnerNfps != null)
            {
                var result = new List<NestPolygon>();
                foreach (var n in cached.InnerNfps)
                    result.Add(n.DeepClone());
                return result;
            }
            return null;
        }

        /// <summary>
        /// Store an outer NFP in the cache.
        /// </summary>
        public void InsertOuter(int aSource, int bSource, double aRotation, double bRotation, NestPolygon nfp)
        {
            var key = MakeKey(aSource, bSource, aRotation, bRotation);
            _cache[key] = new CachedNfp { Nfp = nfp?.DeepClone() };
        }

        /// <summary>
        /// Store an inner NFP in the cache.
        /// </summary>
        public void InsertInner(int aSource, int bSource, double aRotation, double bRotation, List<NestPolygon> nfps)
        {
            var key = MakeKey(aSource, bSource, aRotation, bRotation);
            var cloned = new List<NestPolygon>();
            if (nfps != null)
            {
                foreach (var n in nfps)
                    cloned.Add(n.DeepClone());
            }
            _cache[key] = new CachedNfp { InnerNfps = cloned };
        }

        public void Clear()
        {
            _cache.Clear();
        }

        private class CachedNfp
        {
            public NestPolygon Nfp { get; set; }
            public List<NestPolygon> InnerNfps { get; set; }
        }
    }
}
