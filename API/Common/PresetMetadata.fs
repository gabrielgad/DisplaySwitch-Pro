namespace DisplaySwitchPro

open System
open System.Collections.Generic
open System.Text.Json
open System.Text.Json.Serialization
open AsyncFileOperations

/// Preset categories for organization and filtering
type PresetCategory =
    | Work
    | Gaming
    | Presentation
    | Development
    | Entertainment
    | Productivity
    | Streaming
    | Design
    | Multi_Monitor
    | Single_Monitor
    | TV_Setup
    | Projector
    | Custom of string

    member this.DisplayName =
        match this with
        | Work -> "Work"
        | Gaming -> "Gaming"
        | Presentation -> "Presentation"
        | Development -> "Development"
        | Entertainment -> "Entertainment"
        | Productivity -> "Productivity"
        | Streaming -> "Streaming"
        | Design -> "Design"
        | Multi_Monitor -> "Multi-Monitor"
        | Single_Monitor -> "Single Monitor"
        | TV_Setup -> "TV Setup"
        | Projector -> "Projector"
        | Custom name -> name

/// Compatibility requirements for preset validation
type CompatibilityInfo = {
    MinimumResolution: Resolution option
    RequiredDisplayCount: int
    MaximumDisplayCount: int option
    SupportedOrientations: DisplayOrientation list
    HardwareRequirements: string list
    WindowsVersionRequirement: string option
    Notes: string option
}

/// Usage analytics for preset optimization
type UsageAnalytics = {
    TotalApplications: int
    LastApplied: DateTime option
    AverageApplicationTime: TimeSpan option
    SuccessRate: float
    FailureReasons: Map<string, int>
    UserRating: int option  // 1-5 stars
    BookmarkCount: int
}

/// Enhanced preset metadata with comprehensive information
type PresetMetadata = {
    Category: PresetCategory
    Tags: Set<string>
    Description: string option
    LastUsed: DateTime option
    UsageCount: int
    IsFavorite: bool
    Author: string option
    Version: string
    CreatedAt: DateTime
    ModifiedAt: DateTime
    CompatibilityInfo: CompatibilityInfo
    UsageAnalytics: UsageAnalytics
    CustomProperties: Map<string, string>
}

/// Enhanced display configuration with rich metadata
type EnhancedDisplayConfiguration = {
    Configuration: DisplayConfiguration
    Metadata: PresetMetadata
    ValidationReport: ValidationReport option
    Hash: string option
}

and ValidationReport = {
    IsValid: bool
    Errors: ValidationError list
    Warnings: ValidationError list
    Suggestions: string list
    CompatibilityScore: float
    LastValidated: DateTime
}

/// Search criteria for advanced preset filtering
type PresetSearchCriteria = {
    Query: string option
    Categories: PresetCategory list
    Tags: string list
    Author: string option
    MinUsageCount: int option
    MaxUsageCount: int option
    CreatedAfter: DateTime option
    CreatedBefore: DateTime option
    ModifiedAfter: DateTime option
    ModifiedBefore: DateTime option
    FavoritesOnly: bool
    MinRating: int option
    CompatibleWith: DisplayConfiguration option
    RequiredDisplayCount: int option
    SortBy: SortCriteria
    SortOrder: SortOrder
}

and SortCriteria =
    | Name
    | CreatedDate
    | ModifiedDate
    | LastUsed
    | UsageCount
    | Rating
    | Category
    | CompatibilityScore

and SortOrder =
    | Ascending
    | Descending

/// Preset organization and search functionality
module PresetOrganization =

    /// Default compatibility info for new presets
    let defaultCompatibilityInfo = {
        MinimumResolution = None
        RequiredDisplayCount = 1
        MaximumDisplayCount = None
        SupportedOrientations = [Landscape; Portrait; LandscapeFlipped; PortraitFlipped]
        HardwareRequirements = []
        WindowsVersionRequirement = None
        Notes = None
    }

    /// Default usage analytics for new presets
    let defaultUsageAnalytics = {
        TotalApplications = 0
        LastApplied = None
        AverageApplicationTime = None
        SuccessRate = 0.0
        FailureReasons = Map.empty
        UserRating = None
        BookmarkCount = 0
    }

    /// Create default metadata for a new preset
    let createDefaultMetadata (category: PresetCategory) = {
        Category = category
        Tags = Set.empty
        Description = None
        LastUsed = None
        UsageCount = 0
        IsFavorite = false
        Author = Environment.UserName
        Version = "1.0"
        CreatedAt = DateTime.Now
        ModifiedAt = DateTime.Now
        CompatibilityInfo = defaultCompatibilityInfo
        UsageAnalytics = defaultUsageAnalytics
        CustomProperties = Map.empty
    }

    /// Create enhanced display configuration from basic configuration
    let createEnhancedConfiguration (config: DisplayConfiguration) (metadata: PresetMetadata) = {
        Configuration = config
        Metadata = metadata
        ValidationReport = None
        Hash = None
    }

    /// Update metadata with usage information
    let updateUsageMetadata (metadata: PresetMetadata) (success: bool) (duration: TimeSpan option) (failureReason: string option) =
        let analytics = metadata.UsageAnalytics
        let newTotalApplications = analytics.TotalApplications + 1
        let newSuccessRate =
            if newTotalApplications = 1 then
                if success then 1.0 else 0.0
            else
                let currentSuccesses = analytics.SuccessRate * float analytics.TotalApplications
                let newSuccesses = if success then currentSuccesses + 1.0 else currentSuccesses
                newSuccesses / float newTotalApplications

        let newFailureReasons =
            match failureReason with
            | Some reason when not success ->
                let currentCount = analytics.FailureReasons |> Map.tryFind reason |> Option.defaultValue 0
                Map.add reason (currentCount + 1) analytics.FailureReasons
            | _ -> analytics.FailureReasons

        let newAverageApplicationTime =
            match duration with
            | Some dur ->
                match analytics.AverageApplicationTime with
                | None -> Some dur
                | Some avgTime ->
                    let totalTime = TimeSpan.FromTicks(avgTime.Ticks * int64 analytics.TotalApplications + dur.Ticks)
                    Some (TimeSpan.FromTicks(totalTime.Ticks / int64 newTotalApplications))
            | None -> analytics.AverageApplicationTime

        let updatedAnalytics = {
            analytics with
                TotalApplications = newTotalApplications
                LastApplied = Some DateTime.Now
                AverageApplicationTime = newAverageApplicationTime
                SuccessRate = newSuccessRate
                FailureReasons = newFailureReasons
        }

        {
            metadata with
                LastUsed = Some DateTime.Now
                UsageCount = metadata.UsageCount + 1
                ModifiedAt = DateTime.Now
                UsageAnalytics = updatedAnalytics
        }

    /// Fuzzy string matching for search queries
    let calculateSimilarity (query: string) (text: string) =
        if String.IsNullOrWhiteSpace(query) || String.IsNullOrWhiteSpace(text) then 0.0
        else
            let queryLower = query.ToLowerInvariant().Trim()
            let textLower = text.ToLowerInvariant().Trim()

            // Exact match
            if queryLower = textLower then 1.0
            // Contains match
            elif textLower.Contains(queryLower) then 0.8
            // Word boundary match
            elif textLower.Split(' ') |> Array.exists (fun word -> word.Contains(queryLower)) then 0.6
            // Character overlap
            else
                let queryChars = Set.ofSeq queryLower
                let textChars = Set.ofSeq textLower
                let intersection = Set.intersect queryChars textChars
                let union = Set.union queryChars textChars
                if Set.isEmpty union then 0.0
                else 0.4 * float intersection.Count / float union.Count

    /// Search presets with advanced filtering and fuzzy matching
    let searchPresets (criteria: PresetSearchCriteria) (presets: Map<string, EnhancedDisplayConfiguration>) =
        let filtered =
            presets
            |> Map.toList
            |> List.filter (fun (name, config) ->
                let metadata = config.Metadata

                // Category filter
                let categoryMatch =
                    List.isEmpty criteria.Categories ||
                    List.contains metadata.Category criteria.Categories

                // Tags filter
                let tagsMatch =
                    List.isEmpty criteria.Tags ||
                    criteria.Tags |> List.forall (fun tag -> Set.contains tag metadata.Tags)

                // Author filter
                let authorMatch =
                    match criteria.Author with
                    | None -> true
                    | Some author ->
                        match metadata.Author with
                        | None -> false
                        | Some metadataAuthor -> metadataAuthor.ToLowerInvariant().Contains(author.ToLowerInvariant())

                // Usage count filters
                let usageCountMatch =
                    (criteria.MinUsageCount |> Option.map (fun min -> metadata.UsageCount >= min) |> Option.defaultValue true) &&
                    (criteria.MaxUsageCount |> Option.map (fun max -> metadata.UsageCount <= max) |> Option.defaultValue true)

                // Date filters
                let dateMatch =
                    (criteria.CreatedAfter |> Option.map (fun after -> metadata.CreatedAt >= after) |> Option.defaultValue true) &&
                    (criteria.CreatedBefore |> Option.map (fun before -> metadata.CreatedAt <= before) |> Option.defaultValue true) &&
                    (criteria.ModifiedAfter |> Option.map (fun after -> metadata.ModifiedAt >= after) |> Option.defaultValue true) &&
                    (criteria.ModifiedBefore |> Option.map (fun before -> metadata.ModifiedAt <= before) |> Option.defaultValue true)

                // Favorites filter
                let favoritesMatch = not criteria.FavoritesOnly || metadata.IsFavorite

                // Rating filter
                let ratingMatch =
                    match criteria.MinRating with
                    | None -> true
                    | Some minRating ->
                        match metadata.UsageAnalytics.UserRating with
                        | None -> false
                        | Some rating -> rating >= minRating

                // Display count filter
                let displayCountMatch =
                    match criteria.RequiredDisplayCount with
                    | None -> true
                    | Some count -> config.Configuration.Displays.Length = count

                // Text search with fuzzy matching
                let textMatch =
                    match criteria.Query with
                    | None -> true
                    | Some query ->
                        let nameScore = calculateSimilarity query name
                        let descriptionScore =
                            metadata.Description
                            |> Option.map (calculateSimilarity query)
                            |> Option.defaultValue 0.0
                        let tagScore =
                            metadata.Tags
                            |> Set.toList
                            |> List.map (calculateSimilarity query)
                            |> List.append [0.0]
                            |> List.max
                        max nameScore (max descriptionScore tagScore) > 0.3

                categoryMatch && tagsMatch && authorMatch && usageCountMatch &&
                dateMatch && favoritesMatch && ratingMatch && displayCountMatch && textMatch)

        // Sort results
        let sorted =
            filtered
            |> List.sortWith (fun (name1, config1) (name2, config2) ->
                let metadata1 = config1.Metadata
                let metadata2 = config2.Metadata

                let comparison =
                    match criteria.SortBy with
                    | Name -> String.Compare(name1, name2, StringComparison.OrdinalIgnoreCase)
                    | CreatedDate -> DateTime.Compare(metadata1.CreatedAt, metadata2.CreatedAt)
                    | ModifiedDate -> DateTime.Compare(metadata1.ModifiedAt, metadata2.ModifiedAt)
                    | LastUsed ->
                        let lastUsed1 = metadata1.LastUsed |> Option.defaultValue DateTime.MinValue
                        let lastUsed2 = metadata2.LastUsed |> Option.defaultValue DateTime.MinValue
                        DateTime.Compare(lastUsed1, lastUsed2)
                    | UsageCount -> compare metadata1.UsageCount metadata2.UsageCount
                    | Rating ->
                        let rating1 = metadata1.UsageAnalytics.UserRating |> Option.defaultValue 0
                        let rating2 = metadata2.UsageAnalytics.UserRating |> Option.defaultValue 0
                        compare rating1 rating2
                    | Category -> String.Compare(metadata1.Category.DisplayName, metadata2.Category.DisplayName, StringComparison.OrdinalIgnoreCase)
                    | CompatibilityScore ->
                        let score1 = config1.ValidationReport |> Option.map (fun r -> r.CompatibilityScore) |> Option.defaultValue 0.0
                        let score2 = config2.ValidationReport |> Option.map (fun r -> r.CompatibilityScore) |> Option.defaultValue 0.0
                        compare score1 score2

                match criteria.SortOrder with
                | Ascending -> comparison
                | Descending -> -comparison)

        sorted |> Map.ofList

    /// Filter presets by category
    let filterByCategory (category: PresetCategory) (presets: Map<string, EnhancedDisplayConfiguration>) =
        presets |> Map.filter (fun _ config -> config.Metadata.Category = category)

    /// Filter presets by tags (all tags must be present)
    let filterByTags (tags: string list) (presets: Map<string, EnhancedDisplayConfiguration>) =
        presets |> Map.filter (fun _ config ->
            tags |> List.forall (fun tag -> Set.contains tag config.Metadata.Tags))

    /// Get favorite presets
    let getFavorites (presets: Map<string, EnhancedDisplayConfiguration>) =
        presets |> Map.filter (fun _ config -> config.Metadata.IsFavorite)

    /// Get recently used presets
    let getRecentlyUsed (days: int) (presets: Map<string, EnhancedDisplayConfiguration>) =
        let cutoff = DateTime.Now.AddDays(-float days)
        presets |> Map.filter (fun _ config ->
            config.Metadata.LastUsed
            |> Option.map (fun date -> date > cutoff)
            |> Option.defaultValue false)

    /// Get presets sorted by usage
    let sortByUsage (presets: Map<string, EnhancedDisplayConfiguration>) =
        presets
        |> Map.toList
        |> List.sortByDescending (fun (_, config) ->
            config.Metadata.UsageCount,
            config.Metadata.LastUsed |> Option.defaultValue DateTime.MinValue)
        |> Map.ofList

    /// Get presets by author
    let getPresetsByAuthor (author: string) (presets: Map<string, EnhancedDisplayConfiguration>) =
        presets |> Map.filter (fun _ config ->
            config.Metadata.Author
            |> Option.map (fun a -> a.ToLowerInvariant() = author.ToLowerInvariant())
            |> Option.defaultValue false)

    /// Get all unique tags from presets
    let getAllTags (presets: Map<string, EnhancedDisplayConfiguration>) =
        presets
        |> Map.values
        |> Seq.collect (fun config -> config.Metadata.Tags)
        |> Set.ofSeq

    /// Get all unique categories from presets
    let getAllCategories (presets: Map<string, EnhancedDisplayConfiguration>) =
        presets
        |> Map.values
        |> Seq.map (fun config -> config.Metadata.Category)
        |> Set.ofSeq

/// Advanced preset analytics and reporting
module PresetAnalytics =

    /// Generate comprehensive usage report
    let generateUsageReport (presets: Map<string, EnhancedDisplayConfiguration>) = {|
        TotalPresets = Map.count presets
        TotalUsage =
            presets
            |> Map.values
            |> Seq.sumBy (fun c -> c.Metadata.UsageCount)
        AverageUsage =
            let total = Map.count presets
            if total > 0 then
                let totalUsage = presets |> Map.values |> Seq.sumBy (fun c -> c.Metadata.UsageCount)
                float totalUsage / float total
            else 0.0
        CategoryDistribution =
            presets
            |> Map.values
            |> Seq.groupBy (fun c -> c.Metadata.Category)
            |> Seq.map (fun (cat, configs) -> (cat.DisplayName, Seq.length configs))
            |> Map.ofSeq
        MostUsedPresets =
            presets
            |> PresetOrganization.sortByUsage
            |> Map.toList
            |> List.take (min 5 (Map.count presets))
        UnusedPresets =
            presets
            |> Map.filter (fun _ c -> c.Metadata.UsageCount = 0)
            |> Map.keys
            |> Seq.toList
        FavoriteCount =
            presets
            |> Map.values
            |> Seq.filter (fun c -> c.Metadata.IsFavorite)
            |> Seq.length
        AverageRating =
            let ratings =
                presets
                |> Map.values
                |> Seq.choose (fun c -> c.Metadata.UsageAnalytics.UserRating)
                |> Seq.toList
            if List.isEmpty ratings then None
            else Some (List.average (List.map float ratings))
        TagPopularity =
            presets
            |> Map.values
            |> Seq.collect (fun c -> c.Metadata.Tags)
            |> Seq.groupBy id
            |> Seq.map (fun (tag, occurrences) -> (tag, Seq.length occurrences))
            |> Seq.sortByDescending snd
            |> Seq.take 10
            |> Seq.toList
    |}

    /// Generate compatibility report
    let generateCompatibilityReport (currentConfig: DisplayConfiguration) (presets: Map<string, EnhancedDisplayConfiguration>) = {|
        TotalPresets = Map.count presets
        CompatiblePresets =
            presets
            |> Map.filter (fun _ preset ->
                let displayCount = currentConfig.Displays.Length
                let compatibility = preset.Metadata.CompatibilityInfo
                displayCount >= compatibility.RequiredDisplayCount &&
                (compatibility.MaximumDisplayCount |> Option.map (fun max -> displayCount <= max) |> Option.defaultValue true))
            |> Map.count
        IncompatibleReasons =
            presets
            |> Map.toList
            |> List.choose (fun (name, preset) ->
                let displayCount = currentConfig.Displays.Length
                let compatibility = preset.Metadata.CompatibilityInfo
                if displayCount < compatibility.RequiredDisplayCount then
                    Some (name, sprintf "Requires %d displays, current: %d" compatibility.RequiredDisplayCount displayCount)
                elif compatibility.MaximumDisplayCount |> Option.map (fun max -> displayCount > max) |> Option.defaultValue false then
                    Some (name, sprintf "Maximum %d displays, current: %d" (compatibility.MaximumDisplayCount.Value) displayCount)
                else None)
        RecommendedPresets =
            presets
            |> Map.filter (fun _ preset ->
                let displayCount = currentConfig.Displays.Length
                let compatibility = preset.Metadata.CompatibilityInfo
                displayCount = compatibility.RequiredDisplayCount &&
                preset.Metadata.UsageAnalytics.SuccessRate > 0.8)
            |> PresetOrganization.sortByUsage
            |> Map.toList
            |> List.take 3
    |}