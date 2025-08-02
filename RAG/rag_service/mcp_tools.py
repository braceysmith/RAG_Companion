"""
Tool System for RAG Companion System
Modular tool system that's easy to extend (MCP-inspired but standalone)
"""

import json
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


class MCPToolManager:
    """Manages all available MCP tools"""
    
    def __init__(self):
        self.tools: Dict[str, MCPTool] = {}
        self._register_default_tools()
    
    def _register_default_tools(self):
        """Register default tools"""
        # No default tools registered currently
        # Future tools can be added here:
        # self.register_tool(CalendarTool())
        # self.register_tool(NotesTool())
        pass
    
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