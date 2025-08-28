import os
import cloudinary
import cloudinary.uploader
import cloudinary.api
from typing import Optional, Dict, Any
import logging

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class CloudStorageService:
    """Service for handling cloud storage operations using Cloudinary"""
    
    def __init__(self):
        """Initialize Cloudinary configuration"""
        # Get environment variables
        cloud_name = os.getenv('CLOUDINARY_CLOUD_NAME')
        api_key = os.getenv('CLOUDINARY_API_KEY')
        api_secret = os.getenv('CLOUDINARY_API_SECRET')
        
        if not all([cloud_name, api_key, api_secret]):
            logger.warning("⚠️ Cloudinary credentials not found. Using fallback mode.")
            self.enabled = False
            return
        
        # Configure Cloudinary
        cloudinary.config(
            cloud_name=cloud_name,
            api_key=api_key,
            api_secret=api_secret
        )
        self.enabled = True
        logger.info(f"✅ Cloudinary configured for cloud: {cloud_name}")
    
    async def upload_image(self, 
                          image_data: bytes, 
                          filename: str, 
                          user_id: str,
                          content_id: str,
                          prompt: Optional[str] = None) -> Dict[str, Any]:
        """
        Upload image to Cloudinary and return cloud URL
        
        Args:
            image_data: Raw image bytes
            filename: Original filename
            user_id: User identifier
            content_id: Unique content identifier
            prompt: Generation prompt (optional)
        
        Returns:
            Dict with cloud_url, public_id, and metadata
        """
        if not self.enabled:
            logger.warning("⚠️ Cloudinary not enabled, cannot upload image")
            return {"error": "Cloud storage not configured"}
        
        try:
            # Create unique public ID
            public_id = f"rag_companion/{user_id}/{content_id}"
            
            # Upload to Cloudinary
            upload_result = cloudinary.uploader.upload(
                image_data,
                public_id=public_id,
                resource_type="image",
                overwrite=True,
                tags=[user_id, "ai-generated", "rag-companion"],
                context={
                    "user_id": user_id,
                    "content_id": content_id,
                    "prompt": prompt or "AI generated image",
                    "source": "RAG Companion"
                }
            )
            
            logger.info(f"✅ Image uploaded to Cloudinary: {public_id}")
            
            return {
                "cloud_url": upload_result.get('secure_url'),
                "public_id": upload_result.get('public_id'),
                "width": upload_result.get('width'),
                "height": upload_result.get('height'),
                "format": upload_result.get('format'),
                "bytes": upload_result.get('bytes'),
                "cloudinary_id": upload_result.get('asset_id')
            }
            
        except Exception as e:
            logger.error(f"❌ Error uploading to Cloudinary: {e}")
            return {"error": str(e)}
    
    async def delete_image(self, public_id: str) -> bool:
        """Delete image from Cloudinary"""
        if not self.enabled:
            logger.warning("⚠️ Cloudinary not enabled, cannot delete image")
            return False
        
        try:
            result = cloudinary.uploader.destroy(public_id)
            if result.get('result') == 'ok':
                logger.info(f"✅ Image deleted from Cloudinary: {public_id}")
                return True
            else:
                logger.error(f"❌ Failed to delete image: {result}")
                return False
        except Exception as e:
            logger.error(f"❌ Error deleting from Cloudinary: {e}")
            return False
    
    async def get_image_info(self, public_id: str) -> Optional[Dict[str, Any]]:
        """Get image information from Cloudinary"""
        if not self.enabled:
            return None
        
        try:
            result = cloudinary.api.resource(public_id, resource_type="image")
            return {
                "url": result.get('secure_url'),
                "width": result.get('width'),
                "height": result.get('height'),
                "format": result.get('format'),
                "bytes": result.get('bytes'),
                "created_at": result.get('created_at'),
                "tags": result.get('tags', [])
            }
        except Exception as e:
            logger.error(f"❌ Error getting image info: {e}")
            return None
    
    def is_enabled(self) -> bool:
        """Check if cloud storage is enabled"""
        return self.enabled

# Global instance
cloud_storage = CloudStorageService()
