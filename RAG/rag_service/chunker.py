import re
import tiktoken
from typing import List, Dict, Any, Tuple
from pathlib import Path

class DocumentChunker:
    def __init__(self, encoding_name: str = "cl100k_base"):
        self.encoding = tiktoken.get_encoding(encoding_name)
        
    def chunk_text(self, text: str, doc_title: str = "", section: str = "", 
                   tokens_per_chunk: int = 600, overlap: int = 80,
                   source_path: str = "") -> List[Tuple[str, Dict[str, Any]]]:
        """
        Chunk text into overlapping segments with metadata
        Returns list of (chunk_text, metadata) tuples
        """
        # Normalize whitespace
        text = re.sub(r'\s+', ' ', text.strip())
        
        # Split into sentences
        sentences = self._split_into_sentences(text)
        
        chunks = []
        current_chunk = []
        current_tokens = 0
        
        for sentence in sentences:
            sentence_tokens = len(self.encoding.encode(sentence))
            
            # If adding this sentence would exceed the limit
            if current_tokens + sentence_tokens > tokens_per_chunk and current_chunk:
                # Create chunk with current sentences
                chunk_text = " ".join(current_chunk)
                if doc_title and section:
                    chunk_text = f"{doc_title} - {section}: {chunk_text}"
                elif doc_title:
                    chunk_text = f"{doc_title}: {chunk_text}"
                
                metadata = {
                    "doc_title": doc_title,
                    "section": section,
                    "tags": self._extract_tags(chunk_text),
                    "lang": "en",
                    "user_scope": "global",
                    "safety_level": "public",
                    "token_estimate": len(self.encoding.encode(chunk_text)),
                    "source_path": source_path
                }
                
                chunks.append((chunk_text, metadata))
                
                # Start new chunk with overlap
                overlap_sentences = self._get_overlap_sentences(current_chunk, overlap)
                current_chunk = overlap_sentences + [sentence]
                current_tokens = sum(len(self.encoding.encode(s)) for s in current_chunk)
            else:
                current_chunk.append(sentence)
                current_tokens += sentence_tokens
        
        # Add final chunk if it exists
        if current_chunk:
            chunk_text = " ".join(current_chunk)
            if doc_title and section:
                chunk_text = f"{doc_title} - {section}: {chunk_text}"
            elif doc_title:
                chunk_text = f"{doc_title}: {chunk_text}"
            
            metadata = {
                "doc_title": doc_title,
                "section": section,
                "tags": self._extract_tags(chunk_text),
                "lang": "en",
                "user_scope": "global",
                "safety_level": "public",
                "token_estimate": len(self.encoding.encode(chunk_text)),
                "source_path": source_path
            }
            
            chunks.append((chunk_text, metadata))
        
        return chunks
    
    def _split_into_sentences(self, text: str) -> List[str]:
        """Split text into sentences"""
        # Simple sentence splitting on periods, exclamation marks, and question marks
        sentences = re.split(r'(?<=[.!?])\s+', text)
        return [s.strip() for s in sentences if s.strip()]
    
    def _get_overlap_sentences(self, sentences: List[str], overlap_tokens: int) -> List[str]:
        """Get sentences from the end to create overlap"""
        if not sentences:
            return []
        
        overlap_sentences = []
        current_tokens = 0
        
        for sentence in reversed(sentences):
            sentence_tokens = len(self.encoding.encode(sentence))
            if current_tokens + sentence_tokens <= overlap_tokens:
                overlap_sentences.insert(0, sentence)
                current_tokens += sentence_tokens
            else:
                break
        
        return overlap_sentences
    
    def _extract_tags(self, text: str) -> List[str]:
        """Extract tags from text using simple keyword extraction"""
        # Convert to lowercase and split
        words = re.findall(r'\b\w+\b', text.lower())
        
        # Filter out common words and get word frequencies
        common_words = {
            'the', 'a', 'an', 'and', 'or', 'but', 'in', 'on', 'at', 'to', 'for', 'of', 'with',
            'by', 'is', 'are', 'was', 'were', 'be', 'been', 'have', 'has', 'had', 'do', 'does',
            'did', 'will', 'would', 'could', 'should', 'may', 'might', 'can', 'this', 'that',
            'these', 'those', 'you', 'your', 'yours', 'we', 'our', 'ours', 'they', 'their',
            'theirs', 'he', 'his', 'she', 'her', 'hers', 'it', 'its', 'i', 'my', 'mine', 'me'
        }
        
        # Count word frequencies
        word_counts = {}
        for word in words:
            if len(word) > 3 and word not in common_words:
                word_counts[word] = word_counts.get(word, 0) + 1
        
        # Get top keywords
        tags = sorted(word_counts.items(), key=lambda x: x[1], reverse=True)[:10]
        return [tag[0] for tag in tags]

class DocumentProcessor:
    def __init__(self, chunker: DocumentChunker):
        self.chunker = chunker
    
    def process_file(self, file_path: Path) -> List[Tuple[str, Dict[str, Any]]]:
        """Process a single file and return chunks"""
        if file_path.suffix.lower() == '.txt':
            return self._process_text_file(file_path)
        elif file_path.suffix.lower() == '.md':
            return self._process_markdown_file(file_path)
        else:
            raise ValueError(f"Unsupported file type: {file_path.suffix}")
    
    def _process_text_file(self, file_path: Path) -> List[Tuple[str, Dict[str, Any]]]:
        """Process a plain text file"""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
            
            doc_title = file_path.stem.replace('_', ' ').title()
            return self.chunker.chunk_text(
                content, 
                doc_title=doc_title,
                source_path=str(file_path)
            )
        except Exception as e:
            print(f"Error processing {file_path}: {e}")
            return []
    
    def _process_markdown_file(self, file_path: Path) -> List[Tuple[str, Dict[str, Any]]]:
        """Process a markdown file with section awareness"""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
            
            doc_title = file_path.stem.replace('_', ' ').title()
            sections = self._extract_markdown_sections(content)
            
            all_chunks = []
            for section_title, section_content in sections:
                chunks = self.chunker.chunk_text(
                    section_content,
                    doc_title=doc_title,
                    section=section_title,
                    source_path=str(file_path)
                )
                all_chunks.extend(chunks)
            
            return all_chunks
        except Exception as e:
            print(f"Error processing {file_path}: {e}")
            return []
    
    def _extract_markdown_sections(self, content: str) -> List[Tuple[str, str]]:
        """Extract sections from markdown content"""
        sections = []
        lines = content.split('\n')
        current_section = ""
        current_content = []
        
        for line in lines:
            if line.startswith('#'):
                # Save previous section
                if current_section and current_content:
                    sections.append((current_section, '\n'.join(current_content)))
                
                # Start new section
                current_section = line.lstrip('#').strip()
                current_content = []
            else:
                current_content.append(line)
        
        # Save final section
        if current_section and current_content:
            sections.append((current_section, '\n'.join(current_content)))
        
        return sections if sections else [("", content)]