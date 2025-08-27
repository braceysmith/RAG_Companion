# RAG Companion Image Storage System

This document explains how the new persistent image storage system works in your RAG Companion application.

## Overview

The image storage system now automatically saves all generated images (DALL-E 3) to local storage and database, making them:
- ✅ **Persistent** - Images are saved locally and won't be lost
- ✅ **Retrievable** - Users can access their image history
- ✅ **Searchable** - Images are stored with prompts and metadata
- ✅ **Organized** - Images are linked to user accounts and sessions

## How It Works

### 1. Image Generation Flow

When a user requests an image generation:

1. **DALL-E 3 generates the image** and returns a temporary URL
2. **The RAG server downloads the image** from the temporary URL
3. **Image is saved locally** in `multimedia/images/` directory
4. **Metadata is stored in database** including:
   - User ID
   - Generation prompt
   - Creation timestamp
   - File size and hash
   - Session and turn IDs
5. **Content ID is returned** instead of temporary URL
6. **Unity client displays the stored image** using the content ID

### 2. Storage Structure

```
multimedia/
├── images/           # Generated images stored here
│   ├── {uuid1}.png  # Each image gets unique filename
│   ├── {uuid2}.png
│   └── ...
├── audio/            # Future audio storage
├── video/            # Future video storage
└── documents/        # Future document storage
```

### 3. Database Tables

#### `multimedia_content`
- Stores image metadata and file information
- Links images to users and conversation sessions
- Tracks generation parameters and prompts

#### `conversation_content`
- Links images to specific conversation turns
- Enables conversation history with images
- Stores tool execution parameters

## API Endpoints

### Generate Image
```
POST /generate_image
{
    "prompt": "A beautiful sunset over mountains",
    "size": "1024x1024",
    "user_id": "user123",
    "session_id": "session456",
    "turn_id": "turn789"
}
```

**Response:**
```json
{
    "success": true,
    "content_id": "uuid-here",
    "file_path": "multimedia/images/uuid-here.png",
    "prompt": "A beautiful sunset over mountains",
    "size": "1024x1024",
    "created_at": "2024-01-15T10:30:00Z",
    "file_size": 245760
}
```

### Get User Images
```
GET /user_images/{user_id}?limit=50&offset=0&content_type=image
```

**Response:**
```json
{
    "success": true,
    "user_id": "user123",
    "images": [
        {
            "content_id": "uuid-here",
            "file_name": "uuid-here.png",
            "file_size": 245760,
            "created_at": "2024-01-15T10:30:00Z",
            "generation_prompt": "A beautiful sunset over mountains",
            "generation_tool": "dall-e-3",
            "metadata": {
                "generation_model": "dall-e-3",
                "size": "1024x1024",
                "quality": "standard"
            },
            "tags": ["generated", "ai-art", "dall-e-3"]
        }
    ],
    "total_count": 1,
    "limit": 50,
    "offset": 0
}
```

### Get Specific Image
```
GET /image/{content_id}
```
Returns the actual image file for display.

### Delete Image
```
DELETE /image/{content_id}?user_id={user_id}
```
Deletes image (only by the user who created it).

## Unity Client Integration

### 1. MobileImageManager

The `MobileImageManager` script handles:
- Loading user's image gallery
- Caching images for performance
- Managing image display and interaction
- Handling image deletion

**Setup:**
```csharp
[SerializeField] private MobileImageManager imageManager;

void Start()
{
    imageManager.OnImagesLoaded += OnImagesLoaded;
    imageManager.OnImageSelected += OnImageSelected;
    imageManager.OnError += OnError;
}
```

### 2. Image Gallery UI

The `ImageGalleryItem` script provides:
- Image display with prompt text
- Timestamp and file size information
- View, delete, and share buttons
- Customizable button visibility

### 3. Updated Image Generation

The `MobileRealtimeChat` now:
- Sends session and turn IDs with generation requests
- Handles content IDs instead of temporary URLs
- Displays stored images from local storage
- Falls back to temporary URLs if storage fails

## Configuration

### 1. RAG Server

Ensure your RAG server has:
- Write permissions to create `multimedia/` directory
- Database connection with multimedia tables
- Sufficient storage space for images

### 2. Unity Client

Configure in `MobileImageManager`:
- `ragApiUrl`: Your RAG server URL
- `userId`: User identifier
- `maxImagesToLoad`: Maximum images to load at once
- `autoRefreshOnStart`: Auto-load images on startup

### 3. Database

The system automatically creates required tables:
- `multimedia_content`
- `conversation_content`
- `mcp_tool_executions`

## Benefits

### For Users
- **Never lose generated images** - All images are permanently saved
- **Access image history** - Browse all previously generated images
- **See generation prompts** - Remember what was requested
- **Organized by time** - Images sorted by creation date

### For Developers
- **Persistent storage** - No more temporary URL issues
- **User analytics** - Track image generation patterns
- **Conversation context** - Link images to specific conversations
- **Scalable architecture** - Easy to extend for other media types

## Troubleshooting

### Common Issues

1. **Images not saving**
   - Check server write permissions
   - Verify database connection
   - Check storage directory exists

2. **Images not loading**
   - Verify content ID is correct
   - Check file exists in storage
   - Verify user permissions

3. **Performance issues**
   - Reduce `maxImagesToLoad` value
   - Enable image caching
   - Use pagination for large galleries

### Debug Logging

The system provides detailed logging:
- Image generation and storage
- Database operations
- File system operations
- Error conditions

## Future Enhancements

### Planned Features
- **Image search** by prompt content
- **Image categories** and tags
- **Bulk operations** (delete multiple, export)
- **Image editing** and variations
- **Cloud backup** and sync

### Integration Opportunities
- **User preferences** for image styles
- **Image sharing** between users
- **Image analytics** and insights
- **API rate limiting** and quotas

## Security Considerations

- **User isolation** - Users can only access their own images
- **File validation** - Images are verified before storage
- **Access control** - Delete operations require user authentication
- **Storage limits** - Configurable per-user storage quotas

## Performance Optimization

- **Image caching** - Reduces repeated downloads
- **Lazy loading** - Load images only when needed
- **Compression** - Optimize storage and bandwidth
- **CDN integration** - Fast image delivery worldwide

---

For technical support or questions about the image storage system, please refer to the main RAG Companion documentation or contact the development team.
