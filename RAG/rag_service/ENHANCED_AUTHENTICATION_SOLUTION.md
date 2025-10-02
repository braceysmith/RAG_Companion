# Enhanced Authentication Solution

## Problem Description

When users sign in with the same email through Unity Authentication on different devices, the system creates separate user accounts instead of recognizing them as the same user. This happens because Unity Authentication generates unique user IDs for each device/session, even when using the same email address.

## Root Cause

Unity Authentication Services generates a unique `PlayerId` for each device/session, even when the same email address is used. This results in:
- Multiple user accounts in the database for the same email
- Inconsistent user experience across devices
- Data fragmentation (Sprigs, conversations, etc. are not shared)

## Solution Overview

The enhanced authentication system maps Unity user IDs to consistent user identifiers based on email addresses, ensuring the same email creates the same user account across different devices.

### Key Components

1. **User Mapping Service** (`user_mapping.py`)
   - Maps Unity user IDs to consistent primary user IDs based on email
   - Tracks device information and platform details
   - Handles user statistics and mapping management

2. **Enhanced Authentication API** (`enhanced_auth.py`)
   - Provides `/auth/unity/authenticate` endpoint
   - Maps Unity user IDs to primary user IDs
   - Creates/updates user accounts automatically

3. **Unity Integration** (`EnhancedUserManagement.cs`)
   - Unity script that uses the enhanced authentication system
   - Handles device information and platform detection
   - Provides events for authentication status changes

4. **Migration Tools** (`migrate_user_accounts.py`)
   - Migrates existing user accounts to the new system
   - Handles cleanup of duplicate mappings
   - Provides migration reporting

## How It Works

### 1. User Authentication Flow

```
Unity Device 1 (iOS)          Unity Device 2 (Android)
     │                              │
     │ Sign in with email@test.com  │ Sign in with email@test.com
     │                              │
     ▼                              ▼
Unity User ID: unity_123        Unity User ID: unity_456
     │                              │
     │ Enhanced Auth API             │ Enhanced Auth API
     │                              │
     ▼                              ▼
Primary User ID: user_abc123    Primary User ID: user_abc123
     │                              │
     └──────────────┬───────────────┘
                    │
                    ▼
            Same User Account
            (email@test.com)
```

### 2. Database Schema

#### User Mapping Table
```sql
CREATE TABLE user_mapping (
    id SERIAL PRIMARY KEY,
    email_hash TEXT NOT NULL UNIQUE,
    email TEXT NOT NULL,
    primary_user_id TEXT NOT NULL,
    unity_user_id TEXT NOT NULL,
    device_id TEXT,
    platform TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    last_seen TIMESTAMPTZ DEFAULT NOW(),
    is_active BOOLEAN DEFAULT TRUE
);
```

#### User Accounts Table (Enhanced)
```sql
-- Existing user_accounts table is used
-- Enhanced with email-based primary user IDs
```

### 3. API Endpoints

#### POST `/auth/unity/authenticate`
Authenticates a Unity user and maps to consistent user ID.

**Request:**
```json
{
    "unity_user_id": "unity_user_123",
    "email": "user@example.com",
    "access_token": "unity_access_token",
    "device_id": "device_001",
    "platform": "iOS",
    "player_name": "User Name"
}
```

**Response:**
```json
{
    "success": true,
    "primary_user_id": "user_abc123",
    "email": "user@example.com",
    "is_new_user": false,
    "message": "Welcome back! You have 2 device(s) registered.",
    "user_stats": {
        "total_devices": 2,
        "platforms": ["iOS", "Android"]
    }
}
```

#### GET `/auth/user/{primary_user_id}/mappings`
Get all device mappings for a user.

#### GET `/auth/user/{primary_user_id}/info`
Get user information and statistics.

#### POST `/auth/user/{unity_user_id}/logout`
Logout a Unity user (deactivate their mapping).

## Implementation Steps

### 1. Server Setup

1. **Add the enhanced authentication module to your RAG API:**
   ```python
   from enhanced_auth import router as enhanced_auth_router
   app.include_router(enhanced_auth_router)
   ```

2. **Initialize the user mapping service:**
   ```python
   # This happens automatically when the enhanced_auth module is imported
   ```

3. **Run the migration script (if you have existing users):**
   ```bash
   python3 migrate_user_accounts.py
   ```

### 2. Unity Integration

1. **Add the EnhancedUserManagement script to your Unity project:**
   - Place `EnhancedUserManagement.cs` in your Scripts folder
   - Add it to a GameObject in your scene

2. **Configure the server URL:**
   ```csharp
   [SerializeField] private string serverBaseUrl = "http://your-server:8077";
   ```

3. **Use the enhanced authentication:**
   ```csharp
   // The script automatically handles Unity Authentication events
   // You can also manually trigger authentication:
   EnhancedUserManagement.Instance.AuthenticateWithServer();
   ```

### 3. Testing

Run the test script to verify everything works:
```bash
python3 test_enhanced_auth.py
```

## Benefits

1. **Consistent User Experience**: Same email = same user account across all devices
2. **Data Synchronization**: Sprigs, conversations, and preferences are shared
3. **Device Management**: Track which devices a user has registered
4. **Backward Compatibility**: Existing users can be migrated seamlessly
5. **Scalable**: Handles multiple devices per user efficiently

## Migration Strategy

### For Existing Users

1. **Run the migration script** to map existing accounts:
   ```bash
   python3 migrate_user_accounts.py
   ```

2. **Verify migration results** using the generated report

3. **Update Unity clients** to use the enhanced authentication system

### For New Users

- New users automatically get the enhanced authentication system
- No migration needed for new accounts

## Monitoring and Maintenance

### User Statistics
- Track total devices per user
- Monitor platform distribution
- Identify inactive devices

### Cleanup Tasks
- Periodically clean up inactive mappings
- Remove old device registrations
- Monitor for duplicate accounts

### Admin Tools
- View user mappings: `GET /auth/user/{primary_user_id}/mappings`
- Clean up inactive mappings: `POST /auth/admin/cleanup-inactive`
- Get user statistics: `GET /auth/user/{primary_user_id}/info`

## Security Considerations

1. **Email Hashing**: Email addresses are hashed for privacy
2. **Token Validation**: Unity access tokens are validated
3. **Device Tracking**: Device IDs are tracked for security
4. **Access Control**: Admin endpoints require proper authentication

## Troubleshooting

### Common Issues

1. **"Could not extract email from Unity player info"**
   - Unity Authentication might not provide email directly
   - Implement custom user data or Player Account service

2. **"Authentication request failed"**
   - Check server URL configuration
   - Verify server is running and accessible

3. **"Different primary user ID - mapping failed"**
   - Check email normalization
   - Verify database connection

### Debug Mode

Enable debug logging in Unity:
```csharp
[SerializeField] private bool enableDebugLogging = true;
```

## Future Enhancements

1. **Email Verification**: Add email verification for new accounts
2. **Device Limits**: Implement device limits per user
3. **Push Notifications**: Notify users of new device logins
4. **Account Recovery**: Add account recovery mechanisms
5. **Analytics**: Enhanced user behavior analytics

## Conclusion

The enhanced authentication system solves the multi-device user account issue by mapping Unity user IDs to consistent primary user identifiers based on email addresses. This ensures a seamless user experience across all devices while maintaining data consistency and providing comprehensive user management capabilities.
