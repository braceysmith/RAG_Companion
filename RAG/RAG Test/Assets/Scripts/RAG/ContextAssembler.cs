using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[System.Serializable]
public class TokenBudget
{
    public int totalTokens;
    public int systemTokens;
    public int conversationTokens;
    public int knowledgeTokens;
    public int memoryTokens;
    public int bufferTokens;
    
    public TokenBudget(int total)
    {
        totalTokens = total;
        systemTokens = Mathf.RoundToInt(total * 0.1f);     // 10% for system/policies
        conversationTokens = Mathf.RoundToInt(total * 0.3f); // 30% for conversation
        knowledgeTokens = Mathf.RoundToInt(total * 0.4f);    // 40% for retrieved knowledge
        memoryTokens = Mathf.RoundToInt(total * 0.1f);       // 10% for user memory
        bufferTokens = Mathf.RoundToInt(total * 0.1f);       // 10% buffer for output
    }
}

[System.Serializable]
public class ContextBlock
{
    public string id;
    public string type; // "system", "knowledge", "memory", "conversation"
    public string content;
    public int tokenCount;
    public float priority;
    public Dictionary<string, object> metadata;
    
    public ContextBlock(string blockId, string blockType, string blockContent, int tokens, float blockPriority = 1.0f)
    {
        id = blockId;
        type = blockType;
        content = blockContent;
        tokenCount = tokens;
        priority = blockPriority;
        metadata = new Dictionary<string, object>();
    }
}

[System.Serializable]
public class ConversationMemory
{
    public string role;
    public string content;
    public string timestamp;
    public int tokenCount;
    public Dictionary<string, object> metadata;
    
    public ConversationMemory(string userRole, string userContent, int tokens)
    {
        role = userRole;
        content = userContent;
        timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        tokenCount = tokens;
        metadata = new Dictionary<string, object>();
    }
}

[System.Serializable]
public class UserProfile
{
    public string userId;
    public string name;
    public string location;
    public Dictionary<string, string> preferences;
    public Dictionary<string, object> facts;
    public List<string> interests;
    public DateTime lastUpdated;
    
    public UserProfile(string id)
    {
        userId = id;
        preferences = new Dictionary<string, string>();
        facts = new Dictionary<string, object>();
        interests = new List<string>();
        lastUpdated = DateTime.Now;
    }
}

public class ContextAssembler : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private int maxContextTokens = 8000;
    [SerializeField] private int maxConversationTurns = 10;
    [SerializeField] private bool enableTokenCounting = true;
    // [SerializeField] private bool enableDeduplication = true; // Unused - commented out
    
    [Header("Persona Settings")]
    [SerializeField] private string companionName = "Assistant";
    [SerializeField] private string companionPersonality = "helpful, friendly, and knowledgeable";
    [SerializeField] private string companionRole = "AI companion";
    
    // Token estimation (rough approximation)
    private const float TOKENS_PER_CHAR = 0.25f;
    
    // Memory management
    private List<ConversationMemory> conversationHistory = new List<ConversationMemory>();
    private Dictionary<string, UserProfile> userProfiles = new Dictionary<string, UserProfile>();
    private TokenBudget currentBudget;
    
    // Event handling
    public event Action<string> OnContextAssembled;
    public event Action<TokenBudget> OnTokenBudgetUpdated;
    public event Action<string> OnError;
    
    private void Start()
    {
        currentBudget = new TokenBudget(maxContextTokens);
        OnTokenBudgetUpdated?.Invoke(currentBudget);
    }
    
    public string AssembleContext(string userId, List<RAGResult> retrievedKnowledge, 
                                List<MemoryResult> userMemories, string currentQuery)
    {
        try
        {
            var contextBlocks = new List<ContextBlock>();
            
            // 1. System persona and policies
            var systemBlock = BuildSystemBlock();
            contextBlocks.Add(systemBlock);
            
            // 2. User profile
            var profileBlock = BuildUserProfileBlock(userId);
            if (profileBlock != null)
                contextBlocks.Add(profileBlock);
            
            // 3. Retrieved knowledge
            var knowledgeBlocks = BuildKnowledgeBlocks(retrievedKnowledge);
            contextBlocks.AddRange(knowledgeBlocks);
            
            // 4. User memories
            var memoryBlocks = BuildMemoryBlocks(userMemories);
            contextBlocks.AddRange(memoryBlocks);
            
            // 5. Conversation history
            var conversationBlocks = BuildConversationBlocks();
            contextBlocks.AddRange(conversationBlocks);
            
            // 6. Current query context
            var queryBlock = BuildQueryBlock(currentQuery);
            contextBlocks.Add(queryBlock);
            
            // Apply token budget and prioritization
            var selectedBlocks = ApplyTokenBudget(contextBlocks);
            
            // Assemble final context
            string finalContext = AssembleBlocks(selectedBlocks);
            
            OnContextAssembled?.Invoke(finalContext);
            
            return finalContext;
            
        }
        catch (Exception ex)
        {
            string error = $"Context assembly failed: {ex.Message}";
            OnError?.Invoke(error);
            Debug.LogError($"[ContextAssembler] {error}");
            return GetFallbackContext(userId);
        }
    }
    
    private ContextBlock BuildSystemBlock()
    {
        var systemPrompt = new StringBuilder();
        
        systemPrompt.AppendLine($"You are {companionName}, a {companionPersonality} {companionRole}.");
        systemPrompt.AppendLine("You have access to a knowledge base and user memories to provide helpful, accurate responses.");
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("Guidelines:");
        systemPrompt.AppendLine("- Use retrieved knowledge when relevant to answer questions accurately");
        systemPrompt.AppendLine("- Reference user memories to provide personalized responses");
        systemPrompt.AppendLine("- If you don't know something, admit it rather than guessing");
        systemPrompt.AppendLine("- Be conversational and engaging while staying helpful");
        systemPrompt.AppendLine("- When citing information, mention the source when appropriate");
        systemPrompt.AppendLine("- Keep responses natural and avoid being overly formal");
        
        string content = systemPrompt.ToString();
        int tokens = EstimateTokens(content);
        
        return new ContextBlock("system", "system", content, tokens, 1.0f);
    }
    
    private ContextBlock BuildUserProfileBlock(string userId)
    {
        if (!userProfiles.ContainsKey(userId))
        {
            return null;
        }
        
        var profile = userProfiles[userId];
        var profileBuilder = new StringBuilder();
        
        profileBuilder.AppendLine($"User Profile for {profile.name ?? userId}:");
        
        if (!string.IsNullOrEmpty(profile.location))
        {
            profileBuilder.AppendLine($"Location: {profile.location}");
        }
        
        if (profile.preferences.Count > 0)
        {
            profileBuilder.AppendLine("Preferences:");
            foreach (var pref in profile.preferences)
            {
                profileBuilder.AppendLine($"  - {pref.Key}: {pref.Value}");
            }
        }
        
        if (profile.interests.Count > 0)
        {
            profileBuilder.AppendLine($"Interests: {string.Join(", ", profile.interests)}");
        }
        
        if (profile.facts.Count > 0)
        {
            profileBuilder.AppendLine("Key Facts:");
            foreach (var fact in profile.facts)
            {
                profileBuilder.AppendLine($"  - {fact.Key}: {fact.Value}");
            }
        }
        
        string content = profileBuilder.ToString();
        int tokens = EstimateTokens(content);
        
        return new ContextBlock("user_profile", "memory", content, tokens, 0.9f);
    }
    
    private List<ContextBlock> BuildKnowledgeBlocks(List<RAGResult> retrievedKnowledge)
    {
        var blocks = new List<ContextBlock>();
        
        if (retrievedKnowledge == null || retrievedKnowledge.Count == 0)
        {
            return blocks;
        }
        
        // Group by document to avoid redundancy
        var groupedKnowledge = retrievedKnowledge.GroupBy(r => r.doc_title).ToList();
        
        foreach (var group in groupedKnowledge)
        {
            var knowledgeBuilder = new StringBuilder();
            knowledgeBuilder.AppendLine($"<knowledge_source document=\"{group.Key}\">");
            
            foreach (var result in group.OrderByDescending(r => r.score))
            {
                string section = !string.IsNullOrEmpty(result.section) ? $" - {result.section}" : "";
                knowledgeBuilder.AppendLine($"<knowledge id=\"{result.chunk_id}\" section=\"{result.section}\" score=\"{result.score:F2}\">");
                knowledgeBuilder.AppendLine(result.text);
                knowledgeBuilder.AppendLine("</knowledge>");
            }
            
            knowledgeBuilder.AppendLine("</knowledge_source>");
            
            string content = knowledgeBuilder.ToString();
            int tokens = EstimateTokens(content);
            float priority = group.Max(r => r.score);
            
            blocks.Add(new ContextBlock($"knowledge_{group.Key}", "knowledge", content, tokens, priority));
        }
        
        return blocks;
    }
    
    private List<ContextBlock> BuildMemoryBlocks(List<MemoryResult> userMemories)
    {
        var blocks = new List<ContextBlock>();
        
        if (userMemories == null || userMemories.Count == 0)
        {
            return blocks;
        }
        
        // Group by memory type
        var groupedMemories = userMemories.GroupBy(m => m.memory_type).ToList();
        
        foreach (var group in groupedMemories)
        {
            var memoryBuilder = new StringBuilder();
            memoryBuilder.AppendLine($"<user_memory type=\"{group.Key}\">");
            
            foreach (var memory in group.OrderByDescending(m => m.score))
            {
                memoryBuilder.AppendLine($"<memory id=\"{memory.memory_id}\" score=\"{memory.score:F2}\">");
                memoryBuilder.AppendLine(memory.content);
                memoryBuilder.AppendLine("</memory>");
            }
            
            memoryBuilder.AppendLine("</user_memory>");
            
            string content = memoryBuilder.ToString();
            int tokens = EstimateTokens(content);
            float priority = group.Max(m => m.score);
            
            blocks.Add(new ContextBlock($"memory_{group.Key}", "memory", content, tokens, priority));
        }
        
        return blocks;
    }
    
    private List<ContextBlock> BuildConversationBlocks()
    {
        var blocks = new List<ContextBlock>();
        
        if (conversationHistory.Count == 0)
        {
            return blocks;
        }
        
        // Get recent conversation turns
        var recentHistory = conversationHistory.TakeLast(maxConversationTurns).ToList();
        
        var conversationBuilder = new StringBuilder();
        conversationBuilder.AppendLine("<conversation_history>");
        
        foreach (var turn in recentHistory)
        {
            conversationBuilder.AppendLine($"<turn role=\"{turn.role}\" timestamp=\"{turn.timestamp}\">");
            conversationBuilder.AppendLine(turn.content);
            conversationBuilder.AppendLine("</turn>");
        }
        
        conversationBuilder.AppendLine("</conversation_history>");
        
        string content = conversationBuilder.ToString();
        int tokens = EstimateTokens(content);
        
        blocks.Add(new ContextBlock("conversation", "conversation", content, tokens, 0.8f));
        
        return blocks;
    }
    
    private ContextBlock BuildQueryBlock(string query)
    {
        var queryBuilder = new StringBuilder();
        queryBuilder.AppendLine("<current_query>");
        queryBuilder.AppendLine(query);
        queryBuilder.AppendLine("</current_query>");
        
        string content = queryBuilder.ToString();
        int tokens = EstimateTokens(content);
        
        return new ContextBlock("current_query", "query", content, tokens, 1.0f);
    }
    
    private List<ContextBlock> ApplyTokenBudget(List<ContextBlock> blocks)
    {
        var selectedBlocks = new List<ContextBlock>();
        int remainingTokens = currentBudget.totalTokens - currentBudget.bufferTokens;
        
        // Always include system block
        var systemBlock = blocks.FirstOrDefault(b => b.type == "system");
        if (systemBlock != null)
        {
            selectedBlocks.Add(systemBlock);
            remainingTokens -= systemBlock.tokenCount;
        }
        
        // Always include current query
        var queryBlock = blocks.FirstOrDefault(b => b.type == "query");
        if (queryBlock != null)
        {
            selectedBlocks.Add(queryBlock);
            remainingTokens -= queryBlock.tokenCount;
        }
        
        // Sort remaining blocks by priority and type
        var prioritizedBlocks = blocks
            .Where(b => b.type != "system" && b.type != "query")
            .OrderByDescending(b => b.priority)
            .ThenBy(b => GetTypePriority(b.type))
            .ToList();
        
        // Add blocks within budget
        foreach (var block in prioritizedBlocks)
        {
            if (remainingTokens >= block.tokenCount)
            {
                selectedBlocks.Add(block);
                remainingTokens -= block.tokenCount;
            }
        }
        
        return selectedBlocks;
    }
    
    private int GetTypePriority(string type)
    {
        return type switch
        {
            "knowledge" => 1,
            "memory" => 2,
            "conversation" => 3,
            _ => 4
        };
    }
    
    private string AssembleBlocks(List<ContextBlock> blocks)
    {
        var assembledContext = new StringBuilder();
        
        // Order blocks for final assembly
        var orderedBlocks = blocks.OrderBy(b => GetAssemblyOrder(b.type)).ToList();
        
        foreach (var block in orderedBlocks)
        {
            assembledContext.AppendLine(block.content);
            if (block.type != "query") // Don't add extra spacing after query
            {
                assembledContext.AppendLine();
            }
        }
        
        return assembledContext.ToString();
    }
    
    private int GetAssemblyOrder(string type)
    {
        return type switch
        {
            "system" => 1,
            "memory" => 2,
            "knowledge" => 3,
            "conversation" => 4,
            "query" => 5,
            _ => 6
        };
    }
    
    private string GetFallbackContext(string userId)
    {
        return $"You are {companionName}, a {companionPersonality} {companionRole}. " +
               "Please respond helpfully to the user's question.";
    }
    
    private int EstimateTokens(string text)
    {
        if (!enableTokenCounting)
            return 0;
        
        // Simple token estimation - in production, use tiktoken or similar
        return Mathf.RoundToInt(text.Length * TOKENS_PER_CHAR);
    }
    
    public void AddConversationTurn(string role, string content)
    {
        int tokens = EstimateTokens(content);
        var turn = new ConversationMemory(role, content, tokens);
        conversationHistory.Add(turn);
        
        // Trim history if too long
        while (conversationHistory.Count > maxConversationTurns * 2)
        {
            conversationHistory.RemoveAt(0);
        }
    }
    
    public void UpdateUserProfile(string userId, string name = null, string location = null, 
                                Dictionary<string, string> preferences = null, 
                                Dictionary<string, object> facts = null,
                                List<string> interests = null)
    {
        if (!userProfiles.ContainsKey(userId))
        {
            userProfiles[userId] = new UserProfile(userId);
        }
        
        var profile = userProfiles[userId];
        
        if (name != null) profile.name = name;
        if (location != null) profile.location = location;
        if (preferences != null)
        {
            foreach (var pref in preferences)
            {
                profile.preferences[pref.Key] = pref.Value;
            }
        }
        if (facts != null)
        {
            foreach (var fact in facts)
            {
                profile.facts[fact.Key] = fact.Value;
            }
        }
        if (interests != null)
        {
            profile.interests = interests;
        }
        
        profile.lastUpdated = DateTime.Now;
    }
    
    public void ClearConversationHistory()
    {
        conversationHistory.Clear();
    }
    
    public void SetTokenBudget(int totalTokens)
    {
        maxContextTokens = totalTokens;
        currentBudget = new TokenBudget(totalTokens);
        OnTokenBudgetUpdated?.Invoke(currentBudget);
    }
    
    // Public getters for monitoring
    public int CurrentTokenUsage => conversationHistory.Sum(c => c.tokenCount);
    public int ConversationTurns => conversationHistory.Count;
    public int UserProfilesCount => userProfiles.Count;
    public TokenBudget CurrentBudget => currentBudget;
}