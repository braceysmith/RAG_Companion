"""
Enhanced Wood Type System for DALL-E 3 Integration

This module provides a comprehensive wood type database with detailed descriptions,
grain patterns, color characteristics, and optimized DALL-E 3 prompts for generating
high-quality wood textures.
"""

from typing import Dict, List, Optional, Any
from dataclasses import dataclass
from enum import Enum
import json

class WoodCategory(Enum):
    """Categories for organizing wood types"""
    HARDWOOD = "hardwood"
    SOFTWOOD = "softwood"
    EXOTIC = "exotic"
    RECLAIMED = "reclaimed"
    ENGINEERED = "engineered"

@dataclass
class WoodType:
    """Comprehensive wood type information"""
    wood_type_id: int
    name: str
    description: str
    grain_pattern: str
    color_palette: str
    texture: str
    characteristics: str
    origin: str
    category: WoodCategory
    hardness: int  # 1-10 scale
    workability: str  # easy, moderate, difficult
    common_uses: List[str]
    dall_e_prompt: str
    personality_traits: List[str]  # Associated personality traits

class WoodTypeSystem:
    """Enhanced wood type system with DALL-E 3 integration"""
    
    def __init__(self):
        self.wood_database = self._initialize_wood_database()
    
    def _initialize_wood_database(self) -> Dict[int, WoodType]:
        """Initialize comprehensive wood type database"""
        return {
            # Hardwoods (0-29)
            0: WoodType(
                wood_type_id=0,
                name="Oak",
                description="Classic hardwood with prominent grain patterns and warm golden-brown color",
                grain_pattern="Straight with distinctive ray flecks and cathedral patterns",
                color_palette="Golden brown, honey, amber highlights",
                texture="Medium to coarse grain, open pores",
                characteristics="Durable, strong, distinctive grain patterns",
                origin="North America, Europe",
                category=WoodCategory.HARDWOOD,
                hardness=8,
                workability="moderate",
                common_uses=["Furniture", "Flooring", "Cabinetry", "Architectural millwork"],
                dall_e_prompt="Close-up wood grain texture of Oak, golden brown with honey undertones, straight grain with distinctive ray flecks and cathedral patterns, medium to coarse texture with open pores, natural lighting, high detail, warm wood tones",
                personality_traits=["reliable", "traditional", "strong", "enduring", "classic"]
            ),
            
            1: WoodType(
                wood_type_id=1,
                name="Walnut",
                description="Rich dark brown hardwood with chocolate undertones and elegant grain",
                grain_pattern="Straight with occasional waves and burls",
                color_palette="Dark brown, chocolate, golden highlights",
                texture="Medium to fine grain, smooth finish",
                characteristics="Distinctive grain patterns, often with figure",
                origin="North America, Europe",
                category=WoodCategory.HARDWOOD,
                hardness=7,
                workability="easy",
                common_uses=["Fine furniture", "Gunstocks", "Musical instruments", "Decorative veneers"],
                dall_e_prompt="Close-up wood grain texture of Walnut, rich dark brown with chocolate undertones, straight grain with occasional waves and burls, medium to fine texture, natural lighting, high detail, luxurious wood appearance",
                personality_traits=["sophisticated", "elegant", "refined", "luxurious", "artistic"]
            ),
            
            2: WoodType(
                wood_type_id=2,
                name="Cherry",
                description="Warm reddish-brown hardwood that darkens beautifully with age",
                grain_pattern="Straight, fine, with subtle waves and mineral streaks",
                color_palette="Reddish brown, pink undertones, golden highlights",
                texture="Fine, smooth, closed grain",
                characteristics="Color changes with exposure to light, smooth finish",
                origin="North America",
                category=WoodCategory.HARDWOOD,
                hardness=6,
                workability="easy",
                common_uses=["Fine furniture", "Cabinetry", "Musical instruments", "Turned objects"],
                dall_e_prompt="Close-up wood grain texture of Cherry, warm reddish-brown with pink undertones, straight fine grain with subtle waves, smooth closed grain texture, natural lighting, high detail, warm wood tones",
                personality_traits=["warm", "inviting", "timeless", "elegant", "comforting"]
            ),
            
            3: WoodType(
                wood_type_id=3,
                name="Maple",
                description="Light-colored hardwood with subtle grain and excellent workability",
                grain_pattern="Straight with occasional bird's eye or curly figure",
                color_palette="Cream, light brown, subtle pink undertones",
                texture="Fine, uniform, closed grain",
                characteristics="Hard, dense, takes stain well, can have figure",
                origin="North America, Europe",
                category=WoodCategory.HARDWOOD,
                hardness=9,
                workability="moderate",
                common_uses=["Flooring", "Furniture", "Cutting boards", "Musical instruments"],
                dall_e_prompt="Close-up wood grain texture of Maple, light cream color with subtle pink undertones, straight fine grain with occasional bird's eye figure, uniform closed grain texture, natural lighting, high detail, clean wood appearance",
                personality_traits=["clean", "pure", "versatile", "reliable", "modern"]
            ),
            
            4: WoodType(
                wood_type_id=4,
                name="Mahogany",
                description="Rich reddish-brown tropical hardwood with interlocking grain",
                grain_pattern="Straight to interlocked, ribbon-like appearance",
                color_palette="Reddish brown, golden highlights, dark streaks",
                texture="Medium grain, uniform appearance",
                characteristics="Stable, rot-resistant, beautiful figure",
                origin="Central America, South America, Africa",
                category=WoodCategory.EXOTIC,
                hardness=6,
                workability="easy",
                common_uses=["Fine furniture", "Boat building", "Musical instruments", "Decorative work"],
                dall_e_prompt="Close-up wood grain texture of Mahogany, rich reddish-brown with golden highlights, straight to interlocked grain with ribbon-like appearance, medium grain texture, natural lighting, high detail, tropical wood elegance",
                personality_traits=["exotic", "premium", "sophisticated", "tropical", "luxurious"]
            ),
            
            5: WoodType(
                wood_type_id=5,
                name="Ash",
                description="Light-colored hardwood with prominent grain and excellent shock resistance",
                grain_pattern="Straight with prominent growth rings",
                color_palette="Light brown, cream, subtle gray undertones",
                texture="Coarse, open grain",
                characteristics="Strong, flexible, excellent shock resistance",
                origin="North America, Europe",
                category=WoodCategory.HARDWOOD,
                hardness=7,
                workability="easy",
                common_uses=["Baseball bats", "Tool handles", "Furniture", "Flooring"],
                dall_e_prompt="Close-up wood grain texture of Ash, light brown with cream undertones, straight grain with prominent growth rings, coarse open grain texture, natural lighting, high detail, strong wood appearance",
                personality_traits=["strong", "athletic", "reliable", "flexible", "durable"]
            ),
            
            6: WoodType(
                wood_type_id=6,
                name="Hickory",
                description="Very hard, dense wood with distinctive grain patterns",
                grain_pattern="Straight with occasional waves and knots",
                color_palette="Light brown, reddish undertones, dark streaks",
                texture="Coarse, open grain",
                characteristics="Extremely hard, heavy, excellent strength",
                origin="North America",
                category=WoodCategory.HARDWOOD,
                hardness=10,
                workability="difficult",
                common_uses=["Tool handles", "Flooring", "Furniture", "Smoking wood"],
                dall_e_prompt="Close-up wood grain texture of Hickory, light brown with reddish undertones, straight grain with occasional waves, coarse open grain texture, natural lighting, high detail, extremely hard wood appearance",
                personality_traits=["tough", "resilient", "unbreakable", "determined", "strong-willed"]
            ),
            
            7: WoodType(
                wood_type_id=7,
                name="Pine",
                description="Light-colored softwood with prominent grain and knots",
                grain_pattern="Straight with prominent growth rings and knots",
                color_palette="Light yellow, cream, amber highlights",
                texture="Medium grain, prominent knots",
                characteristics="Lightweight, easy to work, distinctive knot patterns",
                origin="North America, Europe",
                category=WoodCategory.SOFTWOOD,
                hardness=3,
                workability="easy",
                common_uses=["Construction", "Furniture", "Paneling", "Crafts"],
                dall_e_prompt="Close-up wood grain texture of Pine, light yellow with cream undertones, straight grain with prominent growth rings and knots, medium grain texture, natural lighting, high detail, rustic wood appearance",
                personality_traits=["rustic", "natural", "approachable", "humble", "authentic"]
            ),
            
            8: WoodType(
                wood_type_id=8,
                name="Cedar",
                description="Aromatic softwood with reddish-brown color and natural resistance",
                grain_pattern="Straight with occasional waves and knots",
                color_palette="Reddish brown, pink undertones, golden highlights",
                texture="Fine to medium grain, uniform",
                characteristics="Aromatic, rot-resistant, lightweight",
                origin="North America, Europe",
                category=WoodCategory.SOFTWOOD,
                hardness=4,
                workability="easy",
                common_uses=["Outdoor furniture", "Closets", "Decks", "Shingles"],
                dall_e_prompt="Close-up wood grain texture of Cedar, reddish-brown with pink undertones, straight grain with occasional waves, fine to medium grain texture, natural lighting, high detail, aromatic wood appearance",
                personality_traits=["aromatic", "protective", "natural", "outdoor", "resilient"]
            ),
            
            9: WoodType(
                wood_type_id=9,
                name="Teak",
                description="Golden-brown tropical hardwood with natural oils and weather resistance",
                grain_pattern="Straight to wavy, sometimes interlocked",
                color_palette="Golden brown, dark streaks, silver highlights",
                texture="Medium grain, oily feel",
                characteristics="Weather-resistant, high oil content, stable",
                origin="Southeast Asia, India",
                category=WoodCategory.EXOTIC,
                hardness=8,
                workability="moderate",
                common_uses=["Outdoor furniture", "Boat building", "Decking", "Fine furniture"],
                dall_e_prompt="Close-up wood grain texture of Teak, golden brown with dark streaks, straight to wavy grain, medium grain texture with oily appearance, natural lighting, high detail, tropical weather-resistant wood",
                personality_traits=["premium", "weather-resistant", "tropical", "durable", "luxurious"]
            ),
            
            10: WoodType(
                wood_type_id=10,
                name="Bamboo",
                description="Fast-growing grass with unique vertical grain patterns",
                grain_pattern="Vertical lines with occasional horizontal nodes",
                color_palette="Light yellow, cream, subtle green undertones",
                texture="Fine, uniform, vertical grain",
                characteristics="Sustainable, hard, unique appearance",
                origin="Asia, South America",
                category=WoodCategory.ENGINEERED,
                hardness=8,
                workability="moderate",
                common_uses=["Flooring", "Furniture", "Cutting boards", "Decorative items"],
                dall_e_prompt="Close-up wood grain texture of Bamboo, light yellow with cream undertones, vertical grain lines with horizontal nodes, fine uniform texture, natural lighting, high detail, sustainable wood appearance",
                personality_traits=["sustainable", "modern", "eco-friendly", "innovative", "fast-growing"]
            ),
            
            # Add more wood types (11-93) - this is a sample of the first 11
            # The full database would include all 94 wood types with similar detail
        }
    
    def get_wood_type(self, wood_type_id: int) -> Optional[WoodType]:
        """Get wood type by ID"""
        return self.wood_database.get(wood_type_id)
    
    def get_all_wood_types(self) -> Dict[int, WoodType]:
        """Get all wood types"""
        return self.wood_database
    
    def get_wood_types_by_category(self, category: WoodCategory) -> Dict[int, WoodType]:
        """Get wood types by category"""
        return {k: v for k, v in self.wood_database.items() if v.category == category}
    
    def get_wood_types_by_personality_trait(self, trait: str) -> Dict[int, WoodType]:
        """Get wood types that match a personality trait"""
        return {k: v for k, v in self.wood_database.items() if trait.lower() in [t.lower() for t in v.personality_traits]}
    
    def generate_dall_e_prompt(self, wood_type_id: int, personality_traits: List[str] = None, style: str = "natural") -> str:
        """Generate optimized DALL-E 3 prompt for wood texture"""
        wood = self.get_wood_type(wood_type_id)
        if not wood:
            return "Close-up wood grain texture, natural lighting, high detail"
        
        base_prompt = wood.dall_e_prompt
        
        if personality_traits:
            # Enhance prompt based on personality traits
            trait_modifiers = {
                "rustic": "rustic weathered appearance",
                "modern": "clean contemporary finish",
                "luxurious": "premium polished surface",
                "natural": "raw unfinished texture",
                "elegant": "sophisticated refined finish",
                "warm": "warm golden lighting",
                "cool": "cool blue-tinted lighting"
            }
            
            modifiers = [trait_modifiers.get(trait.lower(), "") for trait in personality_traits if trait.lower() in trait_modifiers]
            if modifiers:
                base_prompt += f", {', '.join(modifiers)}"
        
        if style != "natural":
            style_modifiers = {
                "weathered": "weathered aged appearance with patina",
                "polished": "highly polished smooth finish",
                "distressed": "distressed vintage appearance",
                "modern": "contemporary clean finish",
                "rustic": "rustic hand-hewn appearance"
            }
            base_prompt += f", {style_modifiers.get(style, '')}"
        
        return base_prompt
    
    def get_wood_type_info(self, wood_type_id: int) -> Dict[str, Any]:
        """Get comprehensive wood type information as dictionary"""
        wood = self.get_wood_type(wood_type_id)
        if not wood:
            return {"error": "Wood type not found"}
        
        return {
            "woodType": wood.wood_type_id,
            "name": wood.name,
            "description": wood.description,
            "grainPattern": wood.grain_pattern,
            "colorPalette": wood.color_palette,
            "texture": wood.texture,
            "characteristics": wood.characteristics,
            "origin": wood.origin,
            "category": wood.category.value,
            "hardness": wood.hardness,
            "workability": wood.workability,
            "commonUses": wood.common_uses,
            "personalityTraits": wood.personality_traits,
            "dallEPrompt": wood.dall_e_prompt
        }

# Initialize the wood type system
wood_type_system = WoodTypeSystem()
