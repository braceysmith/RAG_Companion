# Sprig Server Integration

This document describes the comprehensive server-side Sprig management system that enables accounts, their sprigs, and sprig content to be saved and recalled from the server.

## Overview

The Sprig server integration provides:
- **Complete Sprig Management**: Create, read, update, delete Sprigs on the server
- **Content Storage**: Store and retrieve Sprig conversations, memories, and preferences
- **Semantic Search**: Search Sprig content using vector embeddings
- **Bi-directional Sync**: Synchronize between Unity client and server
- **User Isolation**: Each user's Sprigs are isolated and secure

## Architecture

### Server Components

1. **SprigManager** (`sprig_management.py`)
   - Core Sprig management logic
   - Database operations for Sprigs and content
   - Vector embedding generation and search

2. **Sprig API** (`sprig_api.py`)
   - REST API endpoints for Sprig operations
   - Request/response validation
   - Error handling and logging

3. **Database Schema**
   - `sprigs` table: Core Sprig data
   - `sprig_content` table: Sprig content with embeddings
   - `sprig_conversations` table: Conversation history

### Unity Components

1. **ServerSprigManager** (`ServerSprigManager.cs`)
   - Unity client for server communication
   - HTTP request handling
   - Response parsing and caching

2. **SprigServerSync** (`SprigServerSync.cs`)
   - Bi-directional synchronization
   - Conflict resolution
   - Auto-sync capabilities

## Database Schema

### Sprigs Table
```sql
CREATE TABLE sprigs (
    sprig_id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT,
    backstory TEXT,
    personality JSONB NOT NULL,
    appearance JSONB NOT NULL,
    skills TEXT[],
    preferences JSONB,
    mood TEXT DEFAULT 'neutral',
    energy_level INTEGER DEFAULT 5,
    is_active BOOLEAN DEFAULT TRUE,
    is_default BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
```

### Sprig Content Table
```sql
CREATE TABLE sprig_content (
    content_id TEXT PRIMARY KEY,
    sprig_id TEXT NOT NULL,
    content_type TEXT NOT NULL,
    content TEXT NOT NULL,
    metadata JSONB,
    embedding VECTOR(1536),
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    FOREIGN KEY (sprig_id) REFERENCES sprigs(sprig_id) ON DELETE CASCADE
);
```

### Sprig Conversations Table
```sql
CREATE TABLE sprig_conversations (
    conversation_id TEXT PRIMARY KEY,
    sprig_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    session_id TEXT,
    message TEXT NOT NULL,
    response TEXT,
    message_type TEXT DEFAULT 'text',
    metadata JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    FOREIGN KEY (sprig_id) REFERENCES sprigs(sprig_id) ON DELETE CASCADE
);
```

## API Endpoints

### Sprig Management

#### Create Sprig
```http
POST /sprig/create
Content-Type: application/json

{
  "user_id": "user123",
  "name": "My Sprig",
  "description": "A helpful companion",
  "backstory": "Born from ancient oak...",
  "personality": {
    "honesty_humility": 7.0,
    "emotionality": 5.0,
    "extraversion": 6.0,
    "agreeableness": 8.0,
    "conscientiousness": 7.0,
    "openness": 6.0
  },
  "appearance": {
    "wood_type": "oak",
    "color_scheme": "natural",
    "size": "medium",
    "texture": "smooth",
    "special_features": ["golden_veins", "moss_patches"]
  }
}
```

#### Get Sprig
```http
GET /sprig/{sprig_id}
```

#### Get User Sprigs
```http
GET /sprig/user/{user_id}?active_only=true
```

#### Get Default Sprig
```http
GET /sprig/user/{user_id}/default
```

#### Set Default Sprig
```http
PUT /sprig/{sprig_id}/default?user_id={user_id}
```

#### Update Sprig
```http
PUT /sprig/{sprig_id}
Content-Type: application/json

{
  "name": "Updated Name",
  "mood": "happy",
  "energy_level": 8
}
```

#### Delete Sprig
```http
DELETE /sprig/{sprig_id}?user_id={user_id}
```

### Content Management

#### Add Content
```http
POST /sprig/{sprig_id}/content
Content-Type: application/json

{
  "content_type": "conversation",
  "content": "User asked about their goals",
  "metadata": {
    "session_id": "session123",
    "importance": "high"
  }
}
```

#### Get Content
```http
GET /sprig/{sprig_id}/content?content_type=conversation&limit=50
```

#### Search Content
```http
POST /sprig/{sprig_id}/search
Content-Type: application/json

{
  "query": "user goals and aspirations",
  "content_type": "conversation",
  "top_k": 5
}
```

### Conversation Management

#### Log Conversation
```http
POST /sprig/{sprig_id}/conversation
Content-Type: application/json

{
  "user_id": "user123",
  "message": "Hello, how are you?",
  "response": "I'm doing well, thank you!",
  "session_id": "session123",
  "message_type": "text"
}
```

#### Get Conversation History
```http
GET /sprig/{sprig_id}/conversations?user_id={user_id}&limit=50
```

## Unity Integration

### Setup

1. **Add ServerSprigManager to your scene**
   ```csharp
   // The singleton will be created automatically
   var serverManager = ServerSprigManager.Instance;
   ```

2. **Configure server URL**
   ```csharp
   serverManager.serverBaseUrl = "http://your-server:8077";
   ```

3. **Add SprigServerSync for automatic synchronization**
   ```csharp
   var sync = gameObject.AddComponent<SprigServerSync>();
   sync.enableAutoSync = true;
   sync.syncInterval = 60f; // Sync every minute
   ```

### Usage Examples

#### Creating a Sprig
```csharp
var personality = new SprigPersonality
{
    honesty_humility = 7.0f,
    emotionality = 5.0f,
    extraversion = 6.0f,
    agreeableness = 8.0f,
    conscientiousness = 7.0f,
    openness = 6.0f
};

var appearance = new SprigAppearance
{
    woodType = "oak",
    colorScheme = "natural",
    size = "medium",
    texture = "smooth",
    specialFeatures = new List<string> { "golden_veins" }
};

ServerSprigManager.Instance.CreateSprig(
    userId: "user123",
    name: "My Sprig",
    description: "A helpful companion",
    backstory: "Born from ancient oak...",
    personality: personality,
    appearance: appearance,
    onSuccess: (sprig) => Debug.Log($"Created Sprig: {sprig.name}"),
    onError: (error) => Debug.LogError($"Error: {error}")
);
```

#### Getting User Sprigs
```csharp
ServerSprigManager.Instance.GetUserSprigs(
    userId: "user123",
    activeOnly: true,
    onSuccess: (sprigs) => {
        foreach (var sprig in sprigs)
        {
            Debug.Log($"Found Sprig: {sprig.name}");
        }
    },
    onError: (error) => Debug.LogError($"Error: {error}")
);
```

#### Adding Content
```csharp
ServerSprigManager.Instance.AddSprigContent(
    sprigId: "sprig123",
    contentType: "conversation",
    content: "User discussed their career goals",
    metadata: new Dictionary<string, object> { {"importance", "high"} },
    onSuccess: (contentId) => Debug.Log($"Added content: {contentId}"),
    onError: (error) => Debug.LogError($"Error: {error}")
);
```

#### Searching Content
```csharp
ServerSprigManager.Instance.SearchSprigContent(
    sprigId: "sprig123",
    query: "career goals and aspirations",
    contentType: "conversation",
    topK: 5,
    onSuccess: (results) => {
        foreach (var result in results)
        {
            Debug.Log($"Found: {result.content} (similarity: {result.similarity})");
        }
    },
    onError: (error) => Debug.LogError($"Error: {error}")
);
```

## Synchronization

The `SprigServerSync` component provides automatic synchronization between Unity and the server:

### Features
- **Auto-sync**: Periodic synchronization (configurable interval)
- **Change-based sync**: Sync when local Sprigs change
- **Startup sync**: Sync when user logs in
- **Conflict resolution**: Multiple strategies for handling conflicts

### Configuration
```csharp
var sync = GetComponent<SprigServerSync>();
sync.enableAutoSync = true;
sync.syncInterval = 60f; // Sync every minute
sync.syncOnStartup = true;
sync.syncOnSprigChange = true;
sync.conflictStrategy = SprigServerSync.ConflictResolutionStrategy.ServerWins;
```

### Conflict Resolution Strategies
- **ServerWins**: Server data takes precedence
- **LocalWins**: Local data takes precedence
- **NewestWins**: Most recently updated data wins
- **Manual**: Require manual resolution

## Security

### User Isolation
- All Sprigs are scoped to specific users
- Users can only access their own Sprigs
- Server validates user ownership for all operations

### Data Privacy
- Content is stored with user-specific access controls
- Embeddings are generated server-side for privacy
- No sensitive data is logged

### Rate Limiting
- Sprig generation is rate-limited (10 requests/minute per user)
- API endpoints have appropriate timeouts
- Request validation prevents malformed data

## Performance

### Caching
- Unity client caches Sprig data locally
- Configurable cache expiry time
- Reduces server requests for frequently accessed data

### Vector Search
- Uses pgvector for efficient similarity search
- Optimized indexes for fast queries
- Configurable result limits

### Database Optimization
- Proper indexing on user_id and sprig_id
- Vector indexes for content search
- Foreign key constraints for data integrity

## Error Handling

### Server Errors
- Comprehensive error responses with details
- Proper HTTP status codes
- Logging for debugging

### Unity Client Errors
- Network error handling
- Timeout management
- Graceful degradation when server unavailable

### Sync Errors
- Retry mechanisms for failed syncs
- Conflict detection and resolution
- Event notifications for sync status

## Monitoring and Logging

### Server Logging
- Request/response logging
- Error tracking
- Performance metrics

### Unity Logging
- Debug logging (configurable)
- Sync status notifications
- Error event handling

## Future Enhancements

### Planned Features
- **Real-time sync**: WebSocket-based real-time updates
- **Bulk operations**: Batch create/update/delete operations
- **Advanced search**: Multi-modal search (text + images)
- **Analytics**: Usage analytics and insights
- **Backup/Restore**: Sprig data backup and restore

### Scalability Improvements
- **Read replicas**: Database read scaling
- **Caching layer**: Redis for improved performance
- **CDN integration**: Content delivery optimization
- **Microservices**: Split into smaller services

## Troubleshooting

### Common Issues

1. **Connection Errors**
   - Check server URL configuration
   - Verify network connectivity
   - Check firewall settings

2. **Authentication Errors**
   - Ensure user is logged in
   - Verify user ID is correct
   - Check server authentication

3. **Sync Issues**
   - Check sync configuration
   - Verify conflict resolution strategy
   - Review sync logs

4. **Performance Issues**
   - Enable caching
   - Adjust sync intervals
   - Check database performance

### Debug Mode
Enable debug logging to troubleshoot issues:
```csharp
ServerSprigManager.Instance.enableDebugLogging = true;
SprigServerSync sync = GetComponent<SprigServerSync>();
sync.enableDebugLogging = true;
```

## Conclusion

The Sprig server integration provides a robust, scalable solution for managing Sprig data across multiple devices and sessions. With comprehensive API endpoints, automatic synchronization, and strong security measures, users can seamlessly work with their Sprigs while maintaining data consistency and privacy.

The system is designed to be extensible and can be enhanced with additional features as needed. The modular architecture allows for easy maintenance and updates without affecting existing functionality.
