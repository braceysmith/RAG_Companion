# Sprig Character Generation Endpoint

## Overview

The `/sprig/generate` endpoint provides isolated character generation functionality for Unity games without interfering with the main chat/conversation system. This endpoint is specifically designed for generating character attributes, personalities, and backstories for game characters.

## Endpoint Details

- **URL**: `POST /sprig/generate`
- **Purpose**: Generate character attributes using LLM without conversation context
- **Isolation**: Completely separate from chat system, no conversation history stored
- **Authentication**: Uses user_id in request body (same pattern as other endpoints)

## Request Format

```json
{
  "prompt": "string (required) - Character generation prompt",
  "user_id": "string (required) - User identifier", 
  "model": "string (optional) - LLM model to use (default: 'gpt-4o')",
  "stream": "boolean (optional) - Whether to stream response (default: false)"
}
```

### Valid Models
- `gpt-4o` (default)
- `gpt-4o-mini`
- `gpt-3.5-turbo`

## Response Format

### Success Response
```json
{
  "response": "Generated character description text",
  "success": true
}
```

### Error Response
```json
{
  "error": "Error message describing what went wrong",
  "success": false
}
```

## Rate Limiting

- **Limit**: 10 requests per minute per user
- **Window**: 60 seconds
- **Exceeded Response**: Returns error with rate limit message

## Example Usage

### Basic Character Generation
```bash
curl -X POST "http://localhost:8077/sprig/generate" \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "Create a brave warrior character with magical abilities",
    "user_id": "unity_game_user_001",
    "model": "gpt-4o-mini"
  }'
```

### Detailed Character with Backstory
```bash
curl -X POST "http://localhost:8077/sprig/generate" \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "Generate a mysterious mage character who was once a noble but now lives as a hermit. Include personality traits, magical abilities, and a detailed backstory.",
    "user_id": "unity_game_user_002",
    "model": "gpt-4o"
  }'
```

## Error Handling

The endpoint handles various error conditions:

1. **Empty prompt**: Returns error if prompt is empty or whitespace
2. **Missing user_id**: Returns error if user_id is not provided
3. **Invalid model**: Returns error if model is not in the valid list
4. **Rate limit exceeded**: Returns error with rate limit message
5. **OpenAI API errors**: Returns error with API error details
6. **Internal server errors**: Returns generic error message

## Key Features

- **Isolation**: No conversation history stored or retrieved
- **No Chat Interference**: Does not affect active chat sessions
- **Direct LLM Access**: Sends prompts directly to OpenAI API
- **Character-Focused**: Optimized for character generation with creative temperature
- **Rate Limited**: Prevents abuse with per-user rate limiting
- **Error Handling**: Comprehensive error handling and validation
- **Timeout Management**: 30-second timeout for API calls

## Testing

Use the provided test script to verify the endpoint:

```bash
cd RAG/rag_service
python test_sprig_endpoint.py
```

## Integration with Unity

This endpoint is designed to be called from Unity games for character generation. The Unity client should:

1. Send character generation prompts to this endpoint
2. Handle the JSON response format
3. Respect rate limiting (implement client-side delays if needed)
4. Handle error responses gracefully
5. Use appropriate user_id for tracking

## Security Considerations

- No authentication headers required (uses user_id in body)
- Rate limiting prevents abuse
- Input validation prevents malformed requests
- No sensitive data stored or logged
- Timeout prevents long-running requests

## Performance

- **Timeout**: 30 seconds maximum
- **Rate Limit**: 10 requests/minute per user
- **Model Support**: Multiple OpenAI models available
- **Response Size**: Up to 1000 tokens (configurable)
- **Temperature**: 0.8 for creative character generation
