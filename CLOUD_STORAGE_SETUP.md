# ☁️ Cloud Storage Setup Guide

This guide will help you set up cloud storage for your RAG Companion app using Cloudinary, eliminating the need for local file storage that gets wiped by Railway's ephemeral filesystem.

## 🎯 **What This Solves**

- ✅ **Persistent Image Storage**: Images survive container restarts
- ✅ **Railway Compatibility**: Works with stateless deployments
- ✅ **Global Access**: Images accessible from anywhere
- ✅ **Automatic Optimization**: Cloudinary handles image optimization
- ✅ **Fallback Support**: Local storage as backup

## 🚀 **Quick Setup**

### **1. Get Cloudinary Account**
1. Go to [Cloudinary.com](https://cloudinary.com)
2. Sign up for free account
3. Get your credentials from Dashboard

### **2. Set Environment Variables**
Add these to your Railway environment variables:

```bash
CLOUDINARY_CLOUD_NAME=your_cloud_name
CLOUDINARY_API_KEY=your_api_key
CLOUDINARY_API_SECRET=your_api_secret
```

### **3. Run Database Migration**
```bash
python3 migrate_to_cloud_storage.py
```

### **4. Restart Your App**
The app will automatically use cloud storage when credentials are available.

## 🔧 **How It Works**

### **Image Generation Flow**
```
1. DALL-E generates image → Temporary URL
2. Download image data → Bytes
3. Upload to Cloudinary → Cloud URL
4. Store metadata in PostgreSQL → Database
5. Return cloud URL to client → Persistent access
```

### **Image Retrieval Flow**
```
1. Client requests image → /image/{content_id}
2. Check database → Get metadata
3. If cloud_url exists → Redirect to Cloudinary
4. If local file exists → Serve local file
5. If neither → 404 error
```

## 📊 **Database Schema Changes**

The `multimedia_content` table now includes:

```sql
ALTER TABLE multimedia_content ADD COLUMN cloud_url TEXT;
ALTER TABLE multimedia_content ADD COLUMN cloud_public_id TEXT;
ALTER TABLE multimedia_content ALTER COLUMN file_path DROP NOT NULL;
```

## 🎨 **Features**

### **Automatic Fallback**
- **Primary**: Cloud storage (Cloudinary)
- **Fallback**: Local storage
- **Graceful degradation** if cloud fails

### **Smart Storage**
- **Generated images**: Stored in cloud
- **User uploads**: Can use either storage
- **Metadata**: Always stored in PostgreSQL

### **Cloudinary Benefits**
- **CDN**: Global image delivery
- **Optimization**: Automatic format/quality optimization
- **Transformations**: On-the-fly image manipulation
- **Analytics**: Usage statistics and insights

## 🔍 **Testing**

### **1. Test Cloud Storage**
```bash
# Check if cloud storage is enabled
curl "https://your-railway-app.com/debug" | jq '.components.cloud_storage'
```

### **2. Test Image Generation**
```bash
# Generate an image (should upload to cloud)
curl -X POST "https://your-railway-app.com/generate_image" \
  -H "Content-Type: application/json" \
  -d '{"prompt": "test image", "user_id": "test"}'
```

### **3. Test Image Retrieval**
```bash
# Get image (should redirect to cloud URL)
curl -I "https://your-railway-app.com/image/{content_id}"
```

## 🚨 **Troubleshooting**

### **Cloud Storage Not Working**
- ✅ Check environment variables
- ✅ Verify Cloudinary credentials
- ✅ Check app logs for errors
- ✅ Ensure `cloud_storage.py` is imported

### **Database Errors**
- ✅ Run migration script
- ✅ Check table structure
- ✅ Verify PostgreSQL connection

### **Images Not Loading**
- ✅ Check cloud_url in database
- ✅ Verify Cloudinary upload success
- ✅ Check app logs for redirects

## 🔄 **Migration from Local Storage**

### **Existing Images**
- **Keep local files** as fallback
- **New images** go to cloud
- **Gradual migration** possible

### **Database Updates**
- **New columns** added automatically
- **Existing data** preserved
- **Backward compatible**

## 📈 **Performance Benefits**

- **Faster delivery**: CDN edge locations
- **Reduced server load**: No file serving
- **Better scalability**: Stateless architecture
- **Global availability**: 24/7 access

## 🔒 **Security**

- **Private uploads**: User-scoped access
- **Secure URLs**: HTTPS only
- **Access control**: User ID validation
- **No public exposure**: Controlled access

## 🎉 **Success Indicators**

When everything is working:
- ✅ `☁️ Image uploaded to cloud:` in logs
- ✅ `☁️ Serving image from cloud:` in logs
- ✅ Images accessible after container restarts
- ✅ `cloud_url` populated in database
- ✅ No more "Failed to Load" errors

## 🆘 **Support**

If you encounter issues:
1. Check app logs for error messages
2. Verify environment variables
3. Test database connection
4. Check Cloudinary dashboard
5. Review this guide again

---

**Happy cloud storing! ☁️✨**
