# Client-Side Name Management System

## 🎯 Overview

The RAG server now supports client-side name management instead of automatic name extraction from user messages. This gives you full control over when and how user names are set, making the system more predictable and easier to integrate with Unity games.

## 🔄 **What Changed**

### **Before (Automatic Name Extraction)**
- Server automatically detected names from messages like "My name is John"
- Names were extracted and stored without explicit client control
- Could lead to inconsistent or unwanted name changes

### **After (Client-Side Name Management)**
- Names are set explicitly via API calls from the client
- No automatic name extraction from conversation messages
- Full control over when and how names are managed

## 📡 **API Endpoints**

### **1. Set User Name**
```http
POST /user/set-name
```

**Request:**
```json
{
  "user_id": "unity_game_user_001",
  "name": "Alice Johnson"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Name set successfully",
  "name": "Alice Johnson"
}
```

### **2. Get User Name**
```http
GET /user/name/{user_id}
```

**Response:**
```json
{
  "success": true,
  "message": "Name retrieved successfully",
  "name": "Alice Johnson"
}
```

**Error Response (no name set):**
```json
{
  "success": false,
  "message": "No name set for this user",
  "name": null
}
```

## 🎮 **Unity Integration**

### **C# Example**
```csharp
public class NameManager : MonoBehaviour
{
    private string serverUrl = "http://localhost:8077";
    
    public async Task<bool> SetUserName(string userId, string name)
    {
        try
        {
            var request = new
            {
                user_id = userId,
                name = name
            };
            
            var response = await httpClient.PostAsJsonAsync($"{serverUrl}/user/set-name", request);
            var result = await response.Content.ReadFromJsonAsync<SetNameResponse>();
            
            if (result.success)
            {
                Debug.Log($"Name set successfully: {result.name}");
                return true;
            }
            else
            {
                Debug.LogError($"Failed to set name: {result.message}");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error setting name: {e.Message}");
            return false;
        }
    }
    
    public async Task<string> GetUserName(string userId)
    {
        try
        {
            var response = await httpClient.GetAsync($"{serverUrl}/user/name/{userId}");
            var result = await response.Content.ReadFromJsonAsync<SetNameResponse>();
            
            if (result.success)
            {
                return result.name;
            }
            else
            {
                Debug.LogWarning($"No name set for user: {result.message}");
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting name: {e.Message}");
            return null;
        }
    }
}

[System.Serializable]
public class SetNameResponse
{
    public bool success;
    public string message;
    public string name;
}
```

### **JavaScript Example**
```javascript
class NameManager {
    constructor(serverUrl = 'http://localhost:8077') {
        this.serverUrl = serverUrl;
    }
    
    async setUserName(userId, name) {
        try {
            const response = await fetch(`${this.serverUrl}/user/set-name`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    user_id: userId,
                    name: name
                })
            });
            
            const result = await response.json();
            
            if (result.success) {
                console.log(`Name set successfully: ${result.name}`);
                return true;
            } else {
                console.error(`Failed to set name: ${result.message}`);
                return false;
            }
        } catch (error) {
            console.error(`Error setting name: ${error.message}`);
            return false;
        }
    }
    
    async getUserName(userId) {
        try {
            const response = await fetch(`${this.serverUrl}/user/name/${userId}`);
            const result = await response.json();
            
            if (result.success) {
                return result.name;
            } else {
                console.warn(`No name set for user: ${result.message}`);
                return null;
            }
        } catch (error) {
            console.error(`Error getting name: ${error.message}`);
            return null;
        }
    }
}
```

## 🔧 **Implementation Details**

### **Name Processing**
- **Trimming**: Names are automatically trimmed of whitespace
- **Title Case**: Names are converted to title case (e.g., "alice johnson" → "Alice Johnson")
- **Validation**: Empty names and missing user IDs are rejected
- **Storage**: Names are stored in the user profile system

### **Integration with RAG**
- Names are still used in RAG responses for personalization
- The `get_user_name()` function continues to work as before
- Names are stored in the same user profile system
- No changes needed to existing RAG query functionality

### **Error Handling**
- **Empty Name**: Returns error if name is empty or whitespace
- **Missing User ID**: Returns error if user_id is not provided
- **Server Errors**: Proper error messages for server issues
- **Network Errors**: Graceful handling of connection issues

## 🧪 **Testing**

Run the test suite to verify functionality:

```bash
cd RAG/rag_service
python test_name_management.py
```

**Test Coverage:**
- Set user name
- Get user name
- Update user name
- Error handling (empty name, missing user_id)
- Name cleaning (trimming, title case)
- Non-existent user handling
- RAG integration testing

## 📊 **Benefits**

1. **Predictable Behavior**: No unexpected name changes from conversation
2. **Client Control**: Full control over when names are set
3. **Better Integration**: Easier to integrate with Unity user management
4. **Consistent State**: Names only change when explicitly set
5. **Error Handling**: Clear error messages for debugging
6. **Backward Compatibility**: Existing RAG functionality unchanged

## 🔄 **Migration Guide**

### **For Existing Users**
- Existing names in the system will continue to work
- No data loss or migration needed
- New names should be set via the API endpoints

### **For New Implementations**
- Use the new API endpoints instead of relying on automatic extraction
- Set names when users register or update their profiles
- Handle name retrieval errors gracefully in your UI

## 🚀 **Usage Examples**

### **Setting a Name During User Registration**
```csharp
// In Unity, when user creates account
public async void OnUserRegistered(string userId, string displayName)
{
    bool success = await nameManager.SetUserName(userId, displayName);
    if (success)
    {
        // Proceed with registration
        Debug.Log("User registered with name");
    }
    else
    {
        // Handle error
        Debug.LogError("Failed to set user name");
    }
}
```

### **Updating Name in Settings**
```csharp
// In Unity, when user updates their name
public async void OnNameUpdated(string userId, string newName)
{
    bool success = await nameManager.SetUserName(userId, newName);
    if (success)
    {
        // Update UI to show new name
        UpdateUserNameDisplay(newName);
    }
}
```

### **Getting Name for Display**
```csharp
// In Unity, when loading user profile
public async void LoadUserProfile(string userId)
{
    string name = await nameManager.GetUserName(userId);
    if (name != null)
    {
        // Display the name
        userNameText.text = name;
    }
    else
    {
        // Show default or prompt for name
        userNameText.text = "Guest User";
    }
}
```

## ⚠️ **Important Notes**

1. **No Automatic Extraction**: The server no longer extracts names from conversation messages
2. **Client Responsibility**: It's now the client's responsibility to manage names
3. **Error Handling**: Always handle the case where no name is set
4. **Validation**: The server validates names but client should also validate
5. **Persistence**: Names are stored persistently in the user profile system

## 🔮 **Future Enhancements**

- **Name Validation**: More sophisticated name validation rules
- **Name History**: Track name changes over time
- **Display Names**: Support for display names vs. real names
- **Name Suggestions**: AI-powered name suggestions
- **Bulk Operations**: Set names for multiple users at once
