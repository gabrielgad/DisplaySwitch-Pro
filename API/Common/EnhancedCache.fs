namespace DisplaySwitchPro

open System
open System.Collections.Generic
open System.Collections.Concurrent
open AsyncFileOperations

/// Cache entry with comprehensive metadata for LRU eviction and analytics
type CacheEntry<'T> = {
    Data: 'T
    LastAccessed: DateTime
    LastModified: DateTime
    AccessCount: int
    IsDirty: bool
    Size: int64
    ExpiresAt: DateTime option
    Tags: Set<string>
}

/// Cache policy configuration for flexible cache behavior
type CachePolicy = {
    MaxAge: TimeSpan option
    MaxEntries: int
    MaxMemorySize: int64
    WriteDelay: TimeSpan
    PersistencePath: string option
    EvictionStrategy: EvictionStrategy
    WriteStrategy: WriteStrategy
}

and EvictionStrategy =
    | LRU  // Least Recently Used
    | LFU  // Least Frequently Used
    | TTL  // Time To Live
    | Hybrid  // Combination approach

and WriteStrategy =
    | WriteThrough    // Immediate write to storage
    | WriteBack       // Delayed write (write-behind)
    | WriteBehind     // Background async write

/// Cache statistics for monitoring and optimization
type CacheStatistics = {
    TotalRequests: int64
    CacheHits: int64
    CacheMisses: int64
    Evictions: int64
    WriteOperations: int64
    AverageAccessTime: TimeSpan
    MemoryUsage: int64
    HitRatio: float
}

/// Enhanced cache with LRU eviction, write-behind strategy, and comprehensive monitoring
type Cache<'Key, 'Value when 'Key : comparison> = {
    Entries: Map<'Key, CacheEntry<'Value>>
    AccessOrder: 'Key list  // LRU tracking (most recent first)
    FrequencyOrder: ('Key * int) list  // LFU tracking
    Policy: CachePolicy
    PendingWrites: Set<'Key>
    Statistics: CacheStatistics
    LastCleanup: DateTime
}

/// Enhanced caching system with LRU eviction and write-behind strategy
module EnhancedCache =

    /// Create a new cache with the specified policy
    let create (policy: CachePolicy) = {
        Entries = Map.empty
        AccessOrder = []
        FrequencyOrder = []
        Policy = policy
        PendingWrites = Set.empty
        Statistics = {
            TotalRequests = 0L
            CacheHits = 0L
            CacheMisses = 0L
            Evictions = 0L
            WriteOperations = 0L
            AverageAccessTime = TimeSpan.Zero
            MemoryUsage = 0L
            HitRatio = 0.0
        }
        LastCleanup = DateTime.Now
    }

    /// Calculate estimated size of a value (can be overridden for specific types)
    let calculateSize<'T> (value: 'T) : int64 =
        try
            // Basic estimation - can be enhanced with specific type handlers
            let json = System.Text.Json.JsonSerializer.Serialize(value)
            int64 (System.Text.Encoding.UTF8.GetByteCount(json))
        with
        | _ -> 1024L  // Default size estimate

    /// Update cache statistics
    let updateStatistics (isHit: bool) (accessTime: TimeSpan) (cache: Cache<'Key, 'Value>) =
        let newStats = {
            cache.Statistics with
                TotalRequests = cache.Statistics.TotalRequests + 1L
                CacheHits = if isHit then cache.Statistics.CacheHits + 1L else cache.Statistics.CacheHits
                CacheMisses = if not isHit then cache.Statistics.CacheMisses + 1L else cache.Statistics.CacheMisses
                AverageAccessTime =
                    let total = cache.Statistics.TotalRequests
                    if total = 0L then accessTime
                    else TimeSpan.FromTicks((cache.Statistics.AverageAccessTime.Ticks * (total - 1L) + accessTime.Ticks) / total)
                HitRatio =
                    let newTotal = cache.Statistics.TotalRequests + 1L
                    let newHits = if isHit then cache.Statistics.CacheHits + 1L else cache.Statistics.CacheHits
                    if newTotal = 0L then 0.0 else float newHits / float newTotal
        }
        { cache with Statistics = newStats }

    /// Check if an entry has expired based on TTL
    let isExpired (entry: CacheEntry<'T>) (policy: CachePolicy) =
        match entry.ExpiresAt with
        | Some expiry -> DateTime.Now > expiry
        | None ->
            match policy.MaxAge with
            | Some maxAge -> DateTime.Now - entry.LastAccessed > maxAge
            | None -> false

    /// Get value from cache with LRU tracking
    let get (key: 'Key) (cache: Cache<'Key, 'Value>) =
        let startTime = DateTime.Now

        match Map.tryFind key cache.Entries with
        | None ->
            let endTime = DateTime.Now
            let updatedCache = updateStatistics false (endTime - startTime) cache
            (None, updatedCache)
        | Some entry ->
            let endTime = DateTime.Now

            // Check if entry has expired
            if isExpired entry cache.Policy then
                let cleanedEntries = Map.remove key cache.Entries
                let cleanedAccessOrder = List.filter ((<>) key) cache.AccessOrder
                let cleanedFrequencyOrder = List.filter (fun (k, _) -> k <> key) cache.FrequencyOrder
                let updatedCache = {
                    cache with
                        Entries = cleanedEntries
                        AccessOrder = cleanedAccessOrder
                        FrequencyOrder = cleanedFrequencyOrder
                } |> updateStatistics false (endTime - startTime)
                (None, updatedCache)
            else
                // Update access metadata
                let updatedEntry = {
                    entry with
                        LastAccessed = DateTime.Now
                        AccessCount = entry.AccessCount + 1
                }

                // Update LRU order (move to front)
                let updatedAccessOrder = key :: (List.filter ((<>) key) cache.AccessOrder)

                // Update LFU order
                let updatedFrequencyOrder =
                    cache.FrequencyOrder
                    |> List.filter (fun (k, _) -> k <> key)
                    |> List.append [(key, updatedEntry.AccessCount)]
                    |> List.sortByDescending snd

                let updatedCache = {
                    cache with
                        Entries = Map.add key updatedEntry cache.Entries
                        AccessOrder = updatedAccessOrder
                        FrequencyOrder = updatedFrequencyOrder
                } |> updateStatistics true (endTime - startTime)

                (Some entry.Data, updatedCache)

    /// Determine which entries to evict based on eviction strategy
    let selectEntriesForEviction (cache: Cache<'Key, 'Value>) (needed: int) =
        match cache.Policy.EvictionStrategy with
        | LRU ->
            cache.AccessOrder
            |> List.rev
            |> List.take (min needed (List.length cache.AccessOrder))
        | LFU ->
            cache.FrequencyOrder
            |> List.sortBy snd
            |> List.take (min needed (List.length cache.FrequencyOrder))
            |> List.map fst
        | TTL ->
            cache.Entries
            |> Map.toList
            |> List.filter (fun (_, entry) -> isExpired entry cache.Policy)
            |> List.take (min needed cache.Entries.Count)
            |> List.map fst
        | Hybrid ->
            // First evict expired entries, then LRU
            let expiredKeys =
                cache.Entries
                |> Map.toList
                |> List.filter (fun (_, entry) -> isExpired entry cache.Policy)
                |> List.map fst

            if expiredKeys.Length >= needed then
                List.take needed expiredKeys
            else
                let lruKeys =
                    cache.AccessOrder
                    |> List.rev
                    |> List.filter (fun k -> not (List.contains k expiredKeys))
                    |> List.take (needed - expiredKeys.Length)
                expiredKeys @ lruKeys

    /// Evict entries if cache limits are exceeded
    let evictIfNecessary (cache: Cache<'Key, 'Value>) =
        let currentSize = cache.Entries |> Map.values |> Seq.sumBy (fun e -> e.Size)
        let currentCount = Map.count cache.Entries

        let sizeExceeded = currentSize > cache.Policy.MaxMemorySize
        let countExceeded = currentCount > cache.Policy.MaxEntries

        if sizeExceeded || countExceeded then
            let entriesNeeded =
                if countExceeded then
                    currentCount - cache.Policy.MaxEntries + 1
                else
                    // Estimate how many entries to remove based on average size
                    let avgSize = if currentCount > 0 then currentSize / int64 currentCount else 1L
                    let sizeToFree = currentSize - cache.Policy.MaxMemorySize
                    int (sizeToFree / avgSize) + 1

            let keysToEvict = selectEntriesForEviction cache entriesNeeded

            let updatedEntries =
                keysToEvict |> List.fold (fun acc key -> Map.remove key acc) cache.Entries
            let updatedAccessOrder =
                keysToEvict |> List.fold (fun acc key -> List.filter ((<>) key) acc) cache.AccessOrder
            let updatedFrequencyOrder =
                keysToEvict |> List.fold (fun acc key -> List.filter (fun (k, _) -> k <> key) acc) cache.FrequencyOrder

            let updatedStats = {
                cache.Statistics with
                    Evictions = cache.Statistics.Evictions + int64 keysToEvict.Length
            }

            {
                cache with
                    Entries = updatedEntries
                    AccessOrder = updatedAccessOrder
                    FrequencyOrder = updatedFrequencyOrder
                    Statistics = updatedStats
            }
        else
            cache

    /// Put value into cache with automatic eviction
    let put (key: 'Key) (value: 'Value) (cache: Cache<'Key, 'Value>) =
        let size = calculateSize value
        let expiresAt =
            match cache.Policy.MaxAge with
            | Some maxAge -> Some (DateTime.Now.Add(maxAge))
            | None -> None

        let newEntry = {
            Data = value
            LastAccessed = DateTime.Now
            LastModified = DateTime.Now
            AccessCount = 1
            IsDirty = true
            Size = size
            ExpiresAt = expiresAt
            Tags = Set.empty
        }

        // Remove existing entry if present
        let cleanedEntries = Map.remove key cache.Entries
        let cleanedAccessOrder = List.filter ((<>) key) cache.AccessOrder
        let cleanedFrequencyOrder = List.filter (fun (k, _) -> k <> key) cache.FrequencyOrder

        // Add new entry
        let updatedEntries = Map.add key newEntry cleanedEntries
        let updatedAccessOrder = key :: cleanedAccessOrder
        let updatedFrequencyOrder = (key, 1) :: cleanedFrequencyOrder
        let updatedPendingWrites = Set.add key cache.PendingWrites

        let cacheWithNewEntry = {
            cache with
                Entries = updatedEntries
                AccessOrder = updatedAccessOrder
                FrequencyOrder = updatedFrequencyOrder
                PendingWrites = updatedPendingWrites
        }

        // Evict if necessary
        evictIfNecessary cacheWithNewEntry

    /// Remove entry from cache
    let remove (key: 'Key) (cache: Cache<'Key, 'Value>) =
        {
            cache with
                Entries = Map.remove key cache.Entries
                AccessOrder = List.filter ((<>) key) cache.AccessOrder
                FrequencyOrder = List.filter (fun (k, _) -> k <> key) cache.FrequencyOrder
                PendingWrites = Set.remove key cache.PendingWrites
        }

    /// Clear all entries from cache
    let clear (cache: Cache<'Key, 'Value>) =
        {
            cache with
                Entries = Map.empty
                AccessOrder = []
                FrequencyOrder = []
                PendingWrites = Set.empty
        }

    /// Get cache size information
    let getSizeInfo (cache: Cache<'Key, 'Value>) =
        let currentSize = cache.Entries |> Map.values |> Seq.sumBy (fun e -> e.Size)
        let currentCount = Map.count cache.Entries
        {|
            CurrentEntries = currentCount
            MaxEntries = cache.Policy.MaxEntries
            CurrentMemoryUsage = currentSize
            MaxMemoryUsage = cache.Policy.MaxMemorySize
            MemoryUtilization = if cache.Policy.MaxMemorySize > 0L then float currentSize / float cache.Policy.MaxMemorySize else 0.0
            EntryUtilization = if cache.Policy.MaxEntries > 0 then float currentCount / float cache.Policy.MaxEntries else 0.0
        |}

    /// Get cache statistics for monitoring
    let getStatistics (cache: Cache<'Key, 'Value>) = cache.Statistics

    /// Cleanup expired entries
    let cleanupExpired (cache: Cache<'Key, 'Value>) =
        let now = DateTime.Now
        let expiredKeys =
            cache.Entries
            |> Map.toList
            |> List.filter (fun (_, entry) -> isExpired entry cache.Policy)
            |> List.map fst

        if List.isEmpty expiredKeys then
            { cache with LastCleanup = now }
        else
            let cleanedCache =
                expiredKeys |> List.fold (fun acc key -> remove key acc) cache

            let updatedStats = {
                cleanedCache.Statistics with
                    Evictions = cleanedCache.Statistics.Evictions + int64 expiredKeys.Length
            }

            { cleanedCache with Statistics = updatedStats; LastCleanup = now }

/// Write-behind caching system for async persistence
module WriteBehindCache =

    open AsyncFileOperations
    open AsyncResultComposition

    /// Flush pending writes to storage using provided write function
    let flushPendingWrites
        (cache: Cache<'Key, 'Value>)
        (writeFunction: 'Key -> 'Value -> Async<Result<unit, PersistenceError>>) = async {

        if Set.isEmpty cache.PendingWrites then
            return Ok cache
        else
            let writeTasks =
                cache.PendingWrites
                |> Set.toList
                |> List.map (fun key -> async {
                    match Map.tryFind key cache.Entries with
                    | Some entry ->
                        let! result = writeFunction key entry.Data
                        match result with
                        | Ok () -> return Ok key
                        | Error e -> return Error (key, e)
                    | None -> return Ok key  // Entry was evicted
                })

            let! results = Async.Parallel writeTasks

            let successfulWrites =
                results
                |> Array.choose (function Ok key -> Some key | Error _ -> None)
                |> Set.ofArray

            let failedWrites =
                results
                |> Array.choose (function Error (key, e) -> Some (key, e) | Ok _ -> None)

            let updatedPendingWrites = Set.difference cache.PendingWrites successfulWrites
            let updatedStats = {
                cache.Statistics with
                    WriteOperations = cache.Statistics.WriteOperations + int64 successfulWrites.Count
            }

            let updatedCache = {
                cache with
                    PendingWrites = updatedPendingWrites
                    Statistics = updatedStats
            }

            if Array.isEmpty failedWrites then
                return Ok updatedCache
            else
                let errorMessages =
                    failedWrites
                    |> Array.map (fun (key, error) -> sprintf "Key %A: %A" key error)
                    |> String.concat "; "
                return Error (WriteFailed("Some writes failed: " + errorMessages, None))
    }

    /// Background flush with periodic execution
    let createPeriodicFlush
        (interval: TimeSpan)
        (writeFunction: 'Key -> 'Value -> Async<Result<unit, PersistenceError>>) =
        fun (cache: Cache<'Key, 'Value>) -> async {
            do! Async.Sleep(int interval.TotalMilliseconds)
            return! flushPendingWrites cache writeFunction
        }

    /// Flush with write batching for improved performance
    let flushWithBatching
        (batchSize: int)
        (cache: Cache<'Key, 'Value>)
        (batchWriteFunction: ('Key * 'Value) list -> Async<Result<unit, PersistenceError>>) = async {

        if Set.isEmpty cache.PendingWrites then
            return Ok cache
        else
            let pendingEntries =
                cache.PendingWrites
                |> Set.toList
                |> List.choose (fun key ->
                    match Map.tryFind key cache.Entries with
                    | Some entry -> Some (key, entry.Data)
                    | None -> None)

            let batches =
                pendingEntries
                |> List.chunkBySize batchSize

            let mutable successfulKeys = Set.empty
            let mutable errors = []

            for batch in batches do
                let! result = batchWriteFunction batch
                match result with
                | Ok () ->
                    successfulKeys <-
                        batch
                        |> List.map fst
                        |> Set.ofList
                        |> Set.union successfulKeys
                | Error e ->
                    errors <- e :: errors

            let updatedPendingWrites = Set.difference cache.PendingWrites successfulKeys
            let updatedStats = {
                cache.Statistics with
                    WriteOperations = cache.Statistics.WriteOperations + int64 successfulKeys.Count
            }

            let updatedCache = {
                cache with
                    PendingWrites = updatedPendingWrites
                    Statistics = updatedStats
            }

            if List.isEmpty errors then
                return Ok updatedCache
            else
                let errorMessage =
                    errors
                    |> List.map (sprintf "%A")
                    |> String.concat "; "
                return Error (WriteFailed("Batch write errors: " + errorMessage, None))
    }