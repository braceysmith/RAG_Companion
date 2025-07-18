using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class MobileContextAssembler : MonoBehaviour
{
    [Header("Mobile Optimization")]
    [SerializeField] private int maxMobileTokens = 4000; // Reduced for mobile
    [SerializeField] private bool enableMobileOptimization = true;
    [SerializeField] private int maxMobileConversationTurns = 5; // Reduced for mobile
    [SerializeField] private bool enableCompression = true;
    
    [Header("Token Budget (Mobile)")]
    // [SerializeField] private float systemTokenPercent = 0.15f; // 15% for mobile - unused
    // [SerializeField] private float conversationTokenPercent = 0.35f; // 35% for mobile - unused
    // [SerializeField] private float knowledgeTokenPercent = 0.35f; // 35% for mobile - unused
    // [SerializeField] private float memoryTokenPercent = 0.10f; // 10% for mobile - unused
    // [SerializeField] private float bufferTokenPercent = 0.05f; // 5% buffer for mobile - unused
    
    [Header("Mobile Context Settings")]
    // [SerializeField] private bool enableSummarization = true; // Commented out - unused
    [SerializeField] private bool enableContextCaching = true;
    [SerializeField] private int maxCacheSize = 50;
    [SerializeField] private float cacheExpiryMinutes = 30f;
    
    // Token budget calculation
    private MobileTokenBudget tokenBudget;
    private Dictionary<string, string> contextCache = new Dictionary<string, string>();
    private Dictionary<string, DateTime> cacheTimestamps = new Dictionary<string, DateTime>();
    
    // Mobile-specific conversation tracking
    private Queue<ConversationTurn> mobileConversationHistory = new Queue<ConversationTurn>();
    private int totalTokensUsed = 0;
    
    private void Start()
    {
        InitializeMobileTokenBudget();
        LogMessage("Mobile Context Assembler initialized");
    }
    
    private void InitializeMobileTokenBudget()
    {
        tokenBudget = new MobileTokenBudget(maxMobileTokens);
        LogMessage($"Mobile token budget initialized: {maxMobileTokens} total tokens");
    }
    
    public string AssembleContextAsync(string userId, List<RAGResult> knowledgeResults, string userQuery)
    {
        try
        {
            // Check cache first
            if (enableContextCaching)
            {
                string cacheKey = GenerateCacheKey(userId, userQuery, knowledgeResults);
                if (contextCache.ContainsKey(cacheKey) && IsCacheValid(cacheKey))
                {
                    LogMessage("Context retrieved from cache");
                    return contextCache[cacheKey];
                }
            }
            
            var contextBuilder = new System.Text.StringBuilder();
            int tokensUsed = 0;
            
            // 1. System Instructions (15%)
            string systemContext = BuildMobileSystemContext();
            int systemTokens = EstimateTokens(systemContext);
            
            if (systemTokens <= tokenBudget.systemTokens)
            {
                contextBuilder.AppendLine(systemContext);
                tokensUsed += systemTokens;
            }
            
            // 2. Conversation History (35%)
            string conversationContext = BuildMobileConversationContext(userId);
            int conversationTokens = EstimateTokens(conversationContext);
            
            if (conversationTokens <= tokenBudget.conversationTokens)
            {
                contextBuilder.AppendLine(conversationContext);
                tokensUsed += conversationTokens;
            }
            else
            {
                // Truncate conversation history to fit budget
                string truncatedConversation = TruncateConversationContext(conversationContext, tokenBudget.conversationTokens);
                contextBuilder.AppendLine(truncatedConversation);
                tokensUsed += tokenBudget.conversationTokens;
            }
            
            // 3. Knowledge Context (35%)
            string knowledgeContext = BuildMobileKnowledgeContext(knowledgeResults);
            int knowledgeTokens = EstimateTokens(knowledgeContext);
            
            if (knowledgeTokens <= tokenBudget.knowledgeTokens)
            {
                contextBuilder.AppendLine(knowledgeContext);
                tokensUsed += knowledgeTokens;
            }
            else
            {
                // Prioritize and truncate knowledge
                string truncatedKnowledge = TruncateKnowledgeContext(knowledgeResults, tokenBudget.knowledgeTokens);
                contextBuilder.AppendLine(truncatedKnowledge);
                tokensUsed += tokenBudget.knowledgeTokens;
            }
            
            // 4. Memory Context (10%)
            string memoryContext = BuildMobileMemoryContext(userId);
            int memoryTokens = EstimateTokens(memoryContext);
            
            if (memoryTokens <= tokenBudget.memoryTokens)
            {
                contextBuilder.AppendLine(memoryContext);
                tokensUsed += memoryTokens;
            }
            
            // 5. Current Query Context
            string queryContext = BuildQueryContext(userQuery);
            contextBuilder.AppendLine(queryContext);
            tokensUsed += EstimateTokens(queryContext);
            
            string finalContext = contextBuilder.ToString();
            
            // Cache the result
            if (enableContextCaching)
            {
                string cacheKey = GenerateCacheKey(userId, userQuery, knowledgeResults);
                CacheContext(cacheKey, finalContext);
            }
            
            totalTokensUsed = tokensUsed;
            LogMessage($"Mobile context assembled: {tokensUsed} tokens used");
            
            return finalContext;
        }
        catch (Exception ex)
        {
            LogError($"Context assembly failed: {ex.Message}");
            return BuildFallbackContext(userQuery);
        }
    }
    
    private string BuildMobileSystemContext()
    {
        return @"<system>
You are a helpful AI assistant optimized for mobile devices. Keep responses concise and relevant.
- Prioritize clarity and brevity
- Use mobile-friendly formatting
- Adapt to limited screen space
- Optimize for touch interaction
- Be mindful of data usage
</system>";
    }
    
    private string BuildMobileConversationContext(string userId)
    {
        if (mobileConversationHistory.Count == 0)
        {
            return "<conversation_history>\nNo previous conversation.\n</conversation_history>";
        }
        
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("<conversation_history>");
        
        // Get recent turns (limited for mobile)
        var recentTurns = mobileConversationHistory.TakeLast(maxMobileConversationTurns);
        
        foreach (var turn in recentTurns)
        {
            if (enableCompression)
            {
                // Compress conversation for mobile
                string compressedUser = CompressText(turn.content);
                contextBuilder.AppendLine($"User: {compressedUser}");
            }
            else
            {
                contextBuilder.AppendLine($"{turn.role}: {turn.content}");
            }
        }
        
        contextBuilder.AppendLine("</conversation_history>");
        return contextBuilder.ToString();
    }
    
    private string BuildMobileKnowledgeContext(List<RAGResult> knowledgeResults)
    {
        if (knowledgeResults == null || knowledgeResults.Count == 0)
        {
            return "<knowledge>\nNo relevant knowledge found.\n</knowledge>";
        }
        
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("<knowledge>");
        
        // Sort by relevance score
        var sortedResults = knowledgeResults.OrderByDescending(r => r.score).ToList();
        
        foreach (var result in sortedResults)
        {
            string content = result.text;
            
            // Compress for mobile if enabled
            if (enableCompression)
            {
                content = CompressText(content);
            }
            
            // Truncate long content for mobile
            if (content.Length > 200)
            {
                content = content.Substring(0, 200) + "...";
            }
            
            contextBuilder.AppendLine($"- {result.doc_title}: {content}");
        }
        
        contextBuilder.AppendLine("</knowledge>");
        return contextBuilder.ToString();
    }
    
    private string BuildMobileMemoryContext(string userId)
    {
        // Simplified memory context for mobile
        return "<memory>\nUser preferences and context will be loaded here.\n</memory>";
    }
    
    private string BuildQueryContext(string userQuery)
    {
        return $@"<current_query>
User is asking: {userQuery}
Device: Mobile
Timestamp: {DateTime.Now:HH:mm}
</current_query>";
    }
    
    private string CompressText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        
        // Simple compression strategies for mobile
        text = text.Replace("  ", " "); // Remove double spaces
        text = text.Replace("\n\n", "\n"); // Remove double newlines
        
        // Remove filler words for mobile optimization
        string[] fillerWords = { "um", "uh", "well", "you know", "like", "actually", "basically" };
        foreach (string filler in fillerWords)
        {
            text = text.Replace($" {filler} ", " ");
        }
        
        return text.Trim();
    }
    
    private string TruncateConversationContext(string context, int maxTokens)
    {
        // Simple truncation - keep most recent content
        string[] lines = context.Split('\n');
        var truncatedLines = new List<string>();
        int currentTokens = 0;
        
        // Keep system tags
        truncatedLines.Add("<conversation_history>");
        
        // Add lines from the end until we hit token limit
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            int lineTokens = EstimateTokens(lines[i]);
            if (currentTokens + lineTokens <= maxTokens)
            {
                truncatedLines.Insert(1, lines[i]);
                currentTokens += lineTokens;
            }
            else
            {
                break;
            }
        }
        
        truncatedLines.Add("</conversation_history>");
        return string.Join("\n", truncatedLines);
    }
    
    private string TruncateKnowledgeContext(List<RAGResult> results, int maxTokens)
    {
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("<knowledge>");
        
        int currentTokens = EstimateTokens("<knowledge>\n</knowledge>");
        
        foreach (var result in results.OrderByDescending(r => r.score))
        {
            string content = result.text;
            if (enableCompression)
            {
                content = CompressText(content);
            }
            
            string line = $"- {result.doc_title}: {content}";
            int lineTokens = EstimateTokens(line);
            
            if (currentTokens + lineTokens <= maxTokens)
            {
                contextBuilder.AppendLine(line);
                currentTokens += lineTokens;
            }
            else
            {
                break;
            }
        }
        
        contextBuilder.AppendLine("</knowledge>");
        return contextBuilder.ToString();
    }
    
    private int EstimateTokens(string text)
    {
        // Simple token estimation for mobile (roughly 4 characters = 1 token)
        return Math.Max(1, text.Length / 4);
    }
    
    private string BuildFallbackContext(string userQuery)
    {
        return $@"<system>
You are a helpful AI assistant. The user is asking: {userQuery}
Please provide a helpful response based on your general knowledge.
</system>";
    }
    
    public void AddConversationTurn(string role, string content)
    {
        var turn = new ConversationTurn(
            id: Guid.NewGuid().ToString(),
            turnRole: role,
            turnContent: content,
            tokens: EstimateTokens(content),
            chunks: new string[0]
        )
        {
            timestamp = DateTime.UtcNow
        };
        
        mobileConversationHistory.Enqueue(turn);
        
        // Limit conversation history for mobile
        while (mobileConversationHistory.Count > maxMobileConversationTurns)
        {
            mobileConversationHistory.Dequeue();
        }
        
        LogMessage($"Added conversation turn: {role} ({turn.tokenCount} tokens)");
    }
    
    public void ClearConversationHistory()
    {
        mobileConversationHistory.Clear();
        totalTokensUsed = 0;
        LogMessage("Conversation history cleared");
    }
    
    private string GenerateCacheKey(string userId, string query, List<RAGResult> results)
    {
        string resultsHash = results != null ? string.Join(",", results.Select(r => r.chunk_id)) : "";
        return $"{userId}_{query.GetHashCode()}_{resultsHash.GetHashCode()}";
    }
    
    private bool IsCacheValid(string cacheKey)
    {
        if (!cacheTimestamps.ContainsKey(cacheKey))
            return false;
        
        return DateTime.UtcNow - cacheTimestamps[cacheKey] < TimeSpan.FromMinutes(cacheExpiryMinutes);
    }
    
    private void CacheContext(string cacheKey, string context)
    {
        // Manage cache size
        if (contextCache.Count >= maxCacheSize)
        {
            // Remove oldest entry
            var oldestKey = cacheTimestamps.OrderBy(kvp => kvp.Value).First().Key;
            contextCache.Remove(oldestKey);
            cacheTimestamps.Remove(oldestKey);
        }
        
        contextCache[cacheKey] = context;
        cacheTimestamps[cacheKey] = DateTime.UtcNow;
    }
    
    public void SetMobileOptimizations(bool enable)
    {
        enableMobileOptimization = enable;
        
        if (enable)
        {
            // Optimize for mobile
            maxMobileTokens = 4000;
            maxMobileConversationTurns = 5;
            enableCompression = true;
            // enableSummarization = true; // Commented out - unused variable
        }
        else
        {
            // Use desktop settings
            maxMobileTokens = 8000;
            maxMobileConversationTurns = 10;
            enableCompression = false;
            // enableSummarization = false; // Commented out - unused variable
        }
        
        InitializeMobileTokenBudget();
        LogMessage($"Mobile optimizations: {(enable ? "Enabled" : "Disabled")}");
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[MobileContextAssembler] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[MobileContextAssembler] {message}");
    }
    
    // Public getters for monitoring
    public int MaxTokens => maxMobileTokens;
    public int TokensUsed => totalTokensUsed;
    public int ConversationTurns => mobileConversationHistory.Count;
    public int CacheSize => contextCache.Count;
    public bool IsMobileOptimized => enableMobileOptimization;
    public float TokenUtilization => maxMobileTokens > 0 ? (float)totalTokensUsed / maxMobileTokens : 0f;
}

[System.Serializable]
public class MobileTokenBudget
{
    public int systemTokens;
    public int conversationTokens;
    public int knowledgeTokens;
    public int memoryTokens;
    public int bufferTokens;
    public int totalTokens;
    
    public MobileTokenBudget(int total)
    {
        totalTokens = total;
        systemTokens = Mathf.RoundToInt(total * 0.15f);     // 15% for system
        conversationTokens = Mathf.RoundToInt(total * 0.35f); // 35% for conversation
        knowledgeTokens = Mathf.RoundToInt(total * 0.35f);    // 35% for knowledge
        memoryTokens = Mathf.RoundToInt(total * 0.10f);       // 10% for memory
        bufferTokens = Mathf.RoundToInt(total * 0.05f);       // 5% buffer
    }
}