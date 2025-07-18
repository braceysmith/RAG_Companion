# Unity RAG Companion System

A comprehensive Retrieval-Augmented Generation (RAG) companion system for Unity that integrates with OpenAI's GPT-4o-mini model.

## Features

- **Real-time AI Companion**: Interactive AI assistant powered by GPT-4o-mini
- **RAG Integration**: Document retrieval and context-aware responses
- **Unity Integration**: Built specifically for Unity with full scene management
- **Flexible UI**: Customizable chat interface with typing indicators
- **Document Management**: Automatic document indexing and retrieval
- **Input System**: Full support for Unity's new Input System
- **Configuration Management**: Persistent settings and easy configuration

## Core Components

### 1. RAGCompanionController
Main controller that orchestrates the entire RAG system:
- Manages conversation flow
- Coordinates between API client and document retrieval
- Handles message queuing and processing status
- Provides event-driven architecture

### 2. OpenAIAPIClient
Handles communication with OpenAI's API:
- GPT-4o-mini integration
- Conversation history management
- Error handling and retry logic
- Configurable model parameters

### 3. RAGDocumentRetrieval
Document indexing and retrieval system:
- TF-IDF based relevance scoring
- Automatic document loading from StreamingAssets
- Support for multiple document formats
- Real-time document updates

### 4. CompanionUI
User interface for companion interaction:
- Real-time chat interface
- Document retrieval visualization
- Processing status indicators
- Keyboard shortcuts and accessibility

### 5. CompanionInputHandler
Input management for companion interaction:
- Unity Input System integration
- Quick command shortcuts (F1, F2, F3)
- UI toggle controls
- Cursor management

### 6. RAGConfiguration
Centralized configuration management:
- Persistent settings storage
- Runtime configuration updates
- Validation and defaults
- ScriptableObject-based configuration

### 7. RAGSceneManager
Scene setup and lifecycle management:
- Automatic system initialization
- Component orchestration
- Cross-scene persistence
- Resource management

## Setup Instructions

### 1. API Key Configuration
```csharp
// Set your OpenAI API key
var companion = FindObjectOfType<RAGCompanionController>();
companion.SetAPIKey("your-api-key-here");
```

### 2. Document Preparation
1. Place your documents in `Assets/StreamingAssets/Documents/`
2. Supported formats: `.txt`, `.md`, `.json`
3. Documents are automatically loaded and indexed on startup

### 3. Scene Setup
1. Add `RAGSceneManager` to your scene
2. Configure camera and UI canvas references
3. Set up companion prefabs (optional)
4. Enable auto-setup for automatic initialization

### 4. Input Configuration
1. Import the included InputSystem actions
2. Configure input mappings in the Input Actions asset
3. Set up keyboard shortcuts as needed

## Usage

### Basic Usage
```csharp
// Send a message to the companion
companion.SendMessage("Hello, can you help me with Unity?");

// Toggle RAG functionality
companion.ToggleRAG(true);

// Clear conversation history
companion.ClearConversation();
```

### Advanced Usage
```csharp
// Add custom documents
companion.AddDocumentToRAG("Custom Guide", "Your content here", new[] {"unity", "tutorial"});

// Listen for responses
companion.OnCompanionResponse += (response) => {
    Debug.Log($"Companion: {response}");
};

// Handle processing status
companion.OnProcessingStatusChanged += (isProcessing) => {
    // Update UI accordingly
};
```

## Configuration

### RAG Settings
- `enableRAG`: Enable/disable RAG functionality
- `maxRetrievalResults`: Maximum documents to retrieve per query
- `relevanceThreshold`: Minimum relevance score for document inclusion
- `maxContextLength`: Maximum context length for API calls

### API Settings
- `modelName`: OpenAI model to use (default: "gpt-4o-mini")
- `temperature`: Response creativity (0.0-2.0)
- `maxTokens`: Maximum response length

### UI Settings
- `maxMessagesDisplayed`: Chat history limit
- `showDocumentRetrievals`: Show retrieved documents in UI
- `enableTypingIndicator`: Show typing indicator during processing

## File Structure

```
Assets/
├── Scripts/
│   ├── RAGCompanionController.cs    # Main controller
│   ├── OpenAIAPIClient.cs           # API integration
│   ├── RAGDocumentRetrieval.cs      # Document system
│   ├── CompanionUI.cs               # User interface
│   ├── CompanionInputHandler.cs     # Input management
│   ├── RAGConfiguration.cs          # Configuration
│   ├── RAGSceneManager.cs           # Scene management
│   └── README.md                    # Documentation
├── StreamingAssets/
│   └── Documents/                   # Document storage
└── Resources/
    └── RAGConfiguration.asset       # Configuration asset
```

## Quick Start

1. **Set API Key**: Configure your OpenAI API key in the RAGConfiguration
2. **Add Documents**: Place your knowledge base files in StreamingAssets/Documents
3. **Setup Scene**: Add RAGSceneManager to your scene and configure references
4. **Test**: Press F1 in play mode to test the companion

## Keyboard Shortcuts

- **F1**: Ask "What can you help me with?"
- **F2**: Ask "Explain the current scene or environment"
- **F3**: Ask "What Unity concepts should I know?"
- **Tab**: Toggle companion UI
- **Escape**: Close companion UI
- **Enter**: Send message in chat

## Troubleshooting

### Common Issues

1. **API Key Not Working**: Verify your OpenAI API key is correctly set
2. **No Documents Found**: Ensure documents are in StreamingAssets/Documents
3. **UI Not Showing**: Check that CompanionUI is properly configured
4. **Input Not Working**: Verify InputSystem actions are properly set up

### Debug Tips

- Enable console logs for detailed system information
- Check the Documents panel to see retrieved documents
- Monitor processing status indicators
- Use the configuration validation system

## Requirements

- Unity 2021.3 or later
- OpenAI API key
- Unity Input System package
- TextMeshPro package

## License

This RAG companion system is provided as-is for educational and development purposes.