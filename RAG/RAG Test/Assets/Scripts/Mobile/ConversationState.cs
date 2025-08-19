using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class ConversationTurn
{
    public string turnId;
    public string role; // "user" or "assistant"
    public string content;
    public DateTime timestamp;
    public int tokenCount;
    public string[] retrievedChunks;
    public Dictionary<string, object> metadata;
    
    public ConversationTurn(string id, string turnRole, string turnContent, int tokens, string[] chunks = null)
    {
        turnId = id;
        role = turnRole;
        content = turnContent;
        timestamp = DateTime.Now;
        tokenCount = tokens;
        retrievedChunks = chunks ?? new string[0];
        metadata = new Dictionary<string, object>();
    }
}

[System.Serializable]
public class UserFact
{
    public string factId;
    public string category;
    public string key;
    public string value;
    public DateTime createdAt;
    public DateTime updatedAt;
    public float confidence;
    public string source;
    
    public UserFact(string category, string key, string value, float confidence = 1.0f, string source = "conversation")
    {
        factId = Guid.NewGuid().ToString();
        this.category = category;
        this.key = key;
        this.value = value;
        this.confidence = confidence;
        this.source = source;
        createdAt = DateTime.Now;
        updatedAt = DateTime.Now;
    }
}

[System.Serializable]
public class ConversationSession
{
    public string sessionId;
    public string userId;
    public DateTime startTime;
    public DateTime? endTime;
    public int turnCount;
    public int totalTokens;
    public List<ConversationTurn> turns;
    public Dictionary<string, object> metadata;
    
    public ConversationSession(string id, string user)
    {
        sessionId = id;
        userId = user;
        startTime = DateTime.Now;
        turnCount = 0;
        totalTokens = 0;
        turns = new List<ConversationTurn>();
        metadata = new Dictionary<string, object>();
    }
}

public class ConversationState : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private int maxTurnsInMemory = 50;
    [SerializeField] private int maxTokensInMemory = 10000;
    [SerializeField] private bool enableFactExtraction = true;
    [SerializeField] private bool enablePersistence = true;
    [SerializeField] private float tokenCountMultiplier = 0.25f;
    
    [Header("Fact Extraction")]
    [SerializeField] private string[] factCategories = { "preference", "personal", "experience", "goal", "interest" };
    [SerializeField] private string[] factKeywords = { "like", "prefer", "want", "need", "am", "have", "work", "live" };
    
    // Current session
    private ConversationSession currentSession;
    private List<UserFact> userFacts = new List<UserFact>();
    private Dictionary<string, float> tokenEstimates = new Dictionary<string, float>();
    
    // Performance tracking
    private int totalTurns = 0;
    private int totalTokens = 0;
    private float averageTokensPerTurn = 0;
    
    // Events
    public event Action<ConversationTurn> OnTurnAdded;
    public event Action<UserFact> OnFactExtracted;
    public event Action<ConversationSession> OnSessionEnded;
    public event Action<string> OnTokenLimitReached;
    public event Action<string> OnError;
    
    private void Start()
    {
        if (enablePersistence)
        {
            LoadPersistedState();
        }
    }
    
    private void OnDestroy()
    {
        if (enablePersistence)
        {
            SavePersistedState();
        }
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && enablePersistence)
        {
            SavePersistedState();
        }
    }
    
    public void StartNewSession(string userId)
    {
        // End current session if active
        if (currentSession != null)
        {
            EndCurrentSession();
        }
        
        // Start new session
        string sessionId = Guid.NewGuid().ToString();
        currentSession = new ConversationSession(sessionId, userId);
        
        LogMessage($"Started new session: {sessionId} for user: {userId}");
    }
    
    public void EndCurrentSession()
    {
        if (currentSession == null)
        {
            LogMessage("No active session to end");
            return;
        }
        
        currentSession.endTime = DateTime.Now;
        currentSession.turnCount = currentSession.turns.Count;
        currentSession.totalTokens = currentSession.turns.Sum(t => t.tokenCount);
        
        LogMessage($"Ended session: {currentSession.sessionId} " +
                  $"({currentSession.turnCount} turns, {currentSession.totalTokens} tokens)");
        
        OnSessionEnded?.Invoke(currentSession);
        
        // Archive session data
        ArchiveSession(currentSession);
        currentSession = null;
    }
    
    public void AddTurn(string role, string content, string[] retrievedChunks = null, 
                       Dictionary<string, object> metadata = null)
    {
        if (currentSession == null)
        {
            LogError("No active session - cannot add turn");
            return;
        }
        
        try
        {
            // Estimate tokens
            int tokenCount = EstimateTokens(content);
            
            // Create turn
            string turnId = Guid.NewGuid().ToString();
            var turn = new ConversationTurn(turnId, role, content, tokenCount, retrievedChunks);
            
            if (metadata != null)
            {
                turn.metadata = metadata;
            }
            
            // Add to session
            currentSession.turns.Add(turn);
            totalTurns++;
            totalTokens += tokenCount;
            
            // Update averages
            averageTokensPerTurn = (float)totalTokens / totalTurns;
            
            // Check token limits
            CheckTokenLimits();
            
            // Extract facts if enabled
            if (enableFactExtraction && role == "user")
            {
                ExtractFacts(content);
            }
            
            LogMessage($"Added {role} turn: {tokenCount} tokens");
            OnTurnAdded?.Invoke(turn);
            
        }
        catch (Exception ex)
        {
            LogError($"Failed to add turn: {ex.Message}");
            OnError?.Invoke($"Failed to add turn: {ex.Message}");
        }
    }
    
    public List<ConversationTurn> GetRecentTurns(int count = 10)
    {
        if (currentSession == null)
            return new List<ConversationTurn>();
        
        return currentSession.turns.TakeLast(count).ToList();
    }
    
    public List<ConversationTurn> GetTurnsByRole(string role, int maxCount = 20)
    {
        if (currentSession == null)
            return new List<ConversationTurn>();
        
        return currentSession.turns
            .Where(t => t.role == role)
            .TakeLast(maxCount)
            .ToList();
    }
    
    public List<ConversationTurn> GetTurnsInTokenBudget(int tokenBudget)
    {
        if (currentSession == null)
            return new List<ConversationTurn>();
        
        var turns = new List<ConversationTurn>();
        int currentTokens = 0;
        
        // Start from most recent and work backwards
        for (int i = currentSession.turns.Count - 1; i >= 0; i--)
        {
            var turn = currentSession.turns[i];
            
            if (currentTokens + turn.tokenCount <= tokenBudget)
            {
                turns.Insert(0, turn);
                currentTokens += turn.tokenCount;
            }
            else
            {
                break;
            }
        }
        
        return turns;
    }
    
    public List<UserFact> GetUserFacts(string category = null, float minConfidence = 0.5f)
    {
        var facts = userFacts.Where(f => f.confidence >= minConfidence);
        
        if (!string.IsNullOrEmpty(category))
        {
            facts = facts.Where(f => f.category == category);
        }
        
        return facts.OrderByDescending(f => f.updatedAt).ToList();
    }
    
    public void UpdateUserFact(string category, string key, string value, float confidence = 1.0f, 
                              string source = "conversation")
    {
        var existingFact = userFacts.FirstOrDefault(f => f.category == category && f.key == key);
        
        if (existingFact != null)
        {
            existingFact.value = value;
            existingFact.confidence = confidence;
            existingFact.source = source;
            existingFact.updatedAt = DateTime.Now;
            
            LogMessage($"Updated fact: {category}.{key} = {value}");
        }
        else
        {
            var newFact = new UserFact(category, key, value, confidence, source);
            userFacts.Add(newFact);
            
            LogMessage($"Added fact: {category}.{key} = {value}");
            OnFactExtracted?.Invoke(newFact);
        }
    }
    
    public void RemoveUserFact(string factId)
    {
        var fact = userFacts.FirstOrDefault(f => f.factId == factId);
        if (fact != null)
        {
            userFacts.Remove(fact);
            LogMessage($"Removed fact: {fact.category}.{fact.key}");
        }
    }
    
    public string GetConversationSummary(int maxTurns = 20)
    {
        if (currentSession == null)
            return "No active conversation";
        
        var recentTurns = GetRecentTurns(maxTurns);
        var summary = new System.Text.StringBuilder();
        
        summary.AppendLine($"Conversation Summary ({recentTurns.Count} turns):");
        summary.AppendLine($"Session: {currentSession.sessionId}");
        summary.AppendLine($"User: {currentSession.userId}");
        summary.AppendLine($"Started: {currentSession.startTime:yyyy-MM-dd HH:mm:ss}");
        summary.AppendLine();
        
        foreach (var turn in recentTurns)
        {
            string timestamp = turn.timestamp.ToString("HH:mm:ss");
            summary.AppendLine($"[{timestamp}] {turn.role}: {turn.content}");
        }
        
        return summary.ToString();
    }
    
    public void ClearConversationHistory()
    {
        if (currentSession != null)
        {
            currentSession.turns.Clear();
            LogMessage("Conversation history cleared");
        }
    }
    
    public void PruneOldTurns()
    {
        if (currentSession == null)
            return;
        
        // Remove turns beyond limit
        while (currentSession.turns.Count > maxTurnsInMemory)
        {
            var removedTurn = currentSession.turns[0];
            currentSession.turns.RemoveAt(0);
            LogMessage($"Pruned old turn: {removedTurn.turnId}");
        }
        
        // Remove turns beyond token limit
        int totalTokensInMemory = currentSession.turns.Sum(t => t.tokenCount);
        while (totalTokensInMemory > maxTokensInMemory && currentSession.turns.Count > 1)
        {
            var removedTurn = currentSession.turns[0];
            currentSession.turns.RemoveAt(0);
            totalTokensInMemory -= removedTurn.tokenCount;
            LogMessage($"Pruned turn for tokens: {removedTurn.turnId}");
        }
    }
    
    private void CheckTokenLimits()
    {
        if (currentSession == null)
            return;
        
        int sessionTokens = currentSession.turns.Sum(t => t.tokenCount);
        
        if (sessionTokens > maxTokensInMemory)
        {
            OnTokenLimitReached?.Invoke($"Session tokens ({sessionTokens}) exceed limit ({maxTokensInMemory})");
            PruneOldTurns();
        }
        
        if (currentSession.turns.Count > maxTurnsInMemory)
        {
            OnTokenLimitReached?.Invoke($"Turn count ({currentSession.turns.Count}) exceeds limit ({maxTurnsInMemory})");
            PruneOldTurns();
        }
    }
    
    private void ExtractFacts(string content)
    {
        try
        {
            string lowerContent = content.ToLowerInvariant();
            
            // Simple fact extraction patterns
            var patterns = new Dictionary<string, string[]>
            {
                ["preference"] = new[] { "i like", "i prefer", "i enjoy", "i love", "i hate", "i dislike" },
                ["personal"] = new[] { "i am", "i'm", "my name is", "i work", "i live" },
                ["experience"] = new[] { "i did", "i went", "i saw", "i tried", "i learned" },
                ["goal"] = new[] { "i want", "i need", "i plan", "i hope", "i wish" },
                ["interest"] = new[] { "interested in", "curious about", "fascinated by" }
            };
            
            foreach (var category in patterns.Keys)
            {
                foreach (var pattern in patterns[category])
                {
                    if (lowerContent.Contains(pattern))
                    {
                        // Extract the fact (simplified implementation)
                        string fact = ExtractFactFromPattern(content, pattern);
                        if (!string.IsNullOrEmpty(fact))
                        {
                            UpdateUserFact(category, pattern, fact, 0.7f, "auto_extraction");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Fact extraction failed: {ex.Message}");
        }
    }
    
    private string ExtractFactFromPattern(string content, string pattern)
    {
        // Simple extraction - in production, use more sophisticated NLP
        int patternIndex = content.ToLowerInvariant().IndexOf(pattern);
        if (patternIndex >= 0)
        {
            int startIndex = patternIndex + pattern.Length;
            int endIndex = content.IndexOf('.', startIndex);
            if (endIndex == -1) endIndex = content.Length;
            
            return content.Substring(startIndex, endIndex - startIndex).Trim();
        }
        
        return string.Empty;
    }
    
    private int EstimateTokens(string text)
    {
        // Simple token estimation
        return Mathf.RoundToInt(text.Length * tokenCountMultiplier);
    }
    
    private void ArchiveSession(ConversationSession session)
    {
        // In production, this would save to persistent storage
        LogMessage($"Archived session: {session.sessionId}");
    }
    
    private void LoadPersistedState()
    {
        // Load from PlayerPrefs or file system
        // Implementation depends on persistence requirements
        LogMessage("Loaded persisted state");
    }
    
    private void SavePersistedState()
    {
        // Save to PlayerPrefs or file system
        // Implementation depends on persistence requirements
        LogMessage("Saved persisted state");
    }
    
    private void LogMessage(string message)
    {
        Debug.Log($"[ConversationState] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[ConversationState] {message}");
    }
    
    // Public getters for monitoring
    public ConversationSession CurrentSession => currentSession;
    public int TotalTurns => totalTurns;
    public int TotalTokens => totalTokens;
    public float AverageTokensPerTurn => averageTokensPerTurn;
    public int UserFactsCount => userFacts.Count;
    public int CurrentSessionTurns => currentSession?.turns.Count ?? 0;
    public int CurrentSessionTokens => currentSession?.turns.Sum(t => t.tokenCount) ?? 0;
}