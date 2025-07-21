"""
MCP Tools for RAG Companion System
Modular tool system that's easy to extend
"""

import json
import requests
import os
from typing import Dict, Any, List, Optional
from abc import ABC, abstractmethod

class MCPTool(ABC):
    """Base class for MCP tools"""
    
    @property
    @abstractmethod
    def name(self) -> str:
        """Tool name"""
        pass
    
    @property
    @abstractmethod
    def description(self) -> str:
        """Tool description for AI"""
        pass
    
    @property
    @abstractmethod
    def parameters(self) -> Dict[str, Any]:
        """Tool parameters schema"""
        pass
    
    @abstractmethod
    async def execute(self, **kwargs) -> Dict[str, Any]:
        """Execute the tool"""
        pass

class WeatherTool(MCPTool):
    """Weather information tool"""
    
    @property
    def name(self) -> str:
        return "get_weather"
    
    @property
    def description(self) -> str:
        return "Get current weather information for a specific location"
    
    @property
    def parameters(self) -> Dict[str, Any]:
        return {
            "type": "object",
            "properties": {
                "location": {
                    "type": "string",
                    "description": "City name or location (e.g., 'New York', 'London, UK')"
                }
            },
            "required": ["location"]
        }
    
    async def execute(self, location: str) -> Dict[str, Any]:
        """Get weather for a location using OpenWeatherMap API"""
        try:
            api_key = os.getenv("OPENWEATHER_API_KEY")
            if not api_key:
                return {
                    "success": False,
                    "error": "Weather API key not configured",
                    "message": "I'd love to check the weather for you, but I need an API key to access weather data."
                }
            
            # OpenWeatherMap API call
            url = f"http://api.openweathermap.org/data/2.5/weather"
            params = {
                "q": location,
                "appid": api_key,
                "units": "metric"  # Celsius
            }
            
            response = requests.get(url, params=params, timeout=10)
            
            if response.status_code == 200:
                data = response.json()
                weather_info = {
                    "success": True,
                    "location": data["name"],
                    "country": data["sys"]["country"],
                    "temperature": data["main"]["temp"],
                    "feels_like": data["main"]["feels_like"],
                    "humidity": data["main"]["humidity"],
                    "description": data["weather"][0]["description"].title(),
                    "wind_speed": data["wind"]["speed"],
                    "message": f"Current weather in {data['name']}, {data['sys']['country']}: {data['weather'][0]['description'].title()}, {data['main']['temp']}°C (feels like {data['main']['feels_like']}°C), humidity {data['main']['humidity']}%"
                }
                return weather_info
            else:
                return {
                    "success": False,
                    "error": f"Weather API error: {response.status_code}",
                    "message": f"I couldn't find weather information for '{location}'. Could you try a different city name?"
                }
                
        except requests.RequestException as e:
            return {
                "success": False,
                "error": f"Network error: {str(e)}",
                "message": "I'm having trouble connecting to the weather service right now."
            }
        except Exception as e:
            return {
                "success": False,
                "error": f"Unexpected error: {str(e)}",
                "message": "Something went wrong while checking the weather."
            }

class MCPToolManager:
    """Manages all available MCP tools"""
    
    def __init__(self):
        self.tools: Dict[str, MCPTool] = {}
        self._register_default_tools()
    
    def _register_default_tools(self):
        """Register default tools"""
        self.register_tool(WeatherTool())
        # Easy to add more tools here:
        # self.register_tool(CalendarTool())
        # self.register_tool(NotesTool())
    
    def register_tool(self, tool: MCPTool):
        """Register a new tool"""
        self.tools[tool.name] = tool
        print(f"Registered MCP tool: {tool.name}")
    
    def get_tool(self, name: str) -> Optional[MCPTool]:
        """Get a tool by name"""
        return self.tools.get(name)
    
    def get_available_tools(self) -> List[Dict[str, Any]]:
        """Get list of available tools for AI"""
        return [
            {
                "name": tool.name,
                "description": tool.description,
                "parameters": tool.parameters
            }
            for tool in self.tools.values()
        ]
    
    async def execute_tool(self, tool_name: str, **kwargs) -> Dict[str, Any]:
        """Execute a tool by name"""
        tool = self.get_tool(tool_name)
        if not tool:
            return {
                "success": False,
                "error": f"Tool '{tool_name}' not found",
                "message": f"I don't have access to a '{tool_name}' tool."
            }
        
        try:
            return await tool.execute(**kwargs)
        except Exception as e:
            return {
                "success": False,
                "error": f"Tool execution error: {str(e)}",
                "message": f"Something went wrong while using the {tool_name} tool."
            }

# Global tool manager instance
tool_manager = MCPToolManager()