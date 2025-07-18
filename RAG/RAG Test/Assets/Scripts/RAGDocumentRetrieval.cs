using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class Document
{
    public string id;
    public string title;
    public string content;
    public string[] tags;
    public DateTime lastModified;
    public float relevanceScore;
    
    public Document(string id, string title, string content, string[] tags = null)
    {
        this.id = id;
        this.title = title;
        this.content = content;
        this.tags = tags ?? new string[0];
        this.lastModified = DateTime.Now;
        this.relevanceScore = 0f;
    }
}

public class RAGDocumentRetrieval : MonoBehaviour
{
    [Header("Document Storage")]
    [SerializeField] private string documentsPath = "Documents";
    [SerializeField] private int maxRetrievalResults = 5;
    [SerializeField] private float relevanceThreshold = 0.1f;
    
    private List<Document> documentDatabase = new List<Document>();
    private Dictionary<string, float> termFrequencies = new Dictionary<string, float>();
    
    public event Action<List<Document>> OnDocumentsRetrieved;
    
    private void Start()
    {
        LoadDocuments();
        BuildTermFrequencyIndex();
    }
    
    private void LoadDocuments()
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, documentsPath);
        
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
            CreateSampleDocuments();
        }
        
        LoadDocumentsFromDirectory(fullPath);
    }
    
    private void LoadDocumentsFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;
        
        string[] files = Directory.GetFiles(directoryPath, "*.txt");
        
        foreach (string filePath in files)
        {
            try
            {
                string content = File.ReadAllText(filePath);
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                
                var document = new Document(
                    Guid.NewGuid().ToString(),
                    fileName,
                    content,
                    ExtractTagsFromContent(content)
                );
                
                documentDatabase.Add(document);
                Debug.Log($"Loaded document: {fileName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading document {filePath}: {ex.Message}");
            }
        }
    }
    
    private void CreateSampleDocuments()
    {
        string documentsDir = Path.Combine(Application.streamingAssetsPath, documentsPath);
        
        var sampleDocs = new Dictionary<string, string>
        {
            ["unity_basics.txt"] = "Unity is a cross-platform game engine developed by Unity Technologies. It supports 2D and 3D graphics, physics simulation, and scripting in C#. Unity uses a component-based architecture where GameObjects can have multiple components attached.",
            
            ["programming_concepts.txt"] = "Object-oriented programming (OOP) is a programming paradigm based on the concept of objects. Key principles include encapsulation, inheritance, and polymorphism. Classes define the structure and behavior of objects.",
            
            ["game_development.txt"] = "Game development involves creating interactive entertainment software. The process includes concept design, prototyping, programming, art creation, testing, and deployment. Popular engines include Unity, Unreal Engine, and Godot.",
            
            ["ai_companion_guide.txt"] = "AI companions in games provide interactive experiences through natural language processing. They can respond to player queries, provide assistance, and maintain conversation context. Integration with large language models enables sophisticated dialogue systems."
        };
        
        foreach (var doc in sampleDocs)
        {
            string filePath = Path.Combine(documentsDir, doc.Key);
            File.WriteAllText(filePath, doc.Value);
        }
    }
    
    private string[] ExtractTagsFromContent(string content)
    {
        var words = content.ToLower()
            .Split(new char[] { ' ', '\n', '\r', '\t', '.', ',', '!', '?', ';', ':' }, 
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 3)
            .GroupBy(word => word)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Take(10)
            .ToArray();
        
        return words;
    }
    
    private void BuildTermFrequencyIndex()
    {
        termFrequencies.Clear();
        
        foreach (var document in documentDatabase)
        {
            var words = document.content.ToLower()
                .Split(new char[] { ' ', '\n', '\r', '\t', '.', ',', '!', '?', ';', ':' }, 
                       StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var word in words)
            {
                if (word.Length > 2)
                {
                    if (termFrequencies.ContainsKey(word))
                        termFrequencies[word]++;
                    else
                        termFrequencies[word] = 1;
                }
            }
        }
    }
    
    public List<Document> RetrieveRelevantDocuments(string query)
    {
        var queryTerms = query.ToLower()
            .Split(new char[] { ' ', '\n', '\r', '\t', '.', ',', '!', '?', ';', ':' }, 
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length > 2)
            .ToArray();
        
        foreach (var document in documentDatabase)
        {
            document.relevanceScore = CalculateRelevanceScore(document, queryTerms);
        }
        
        var relevantDocuments = documentDatabase
            .Where(doc => doc.relevanceScore > relevanceThreshold)
            .OrderByDescending(doc => doc.relevanceScore)
            .Take(maxRetrievalResults)
            .ToList();
        
        OnDocumentsRetrieved?.Invoke(relevantDocuments);
        
        return relevantDocuments;
    }
    
    private float CalculateRelevanceScore(Document document, string[] queryTerms)
    {
        float score = 0f;
        string documentContent = document.content.ToLower();
        
        foreach (var term in queryTerms)
        {
            int termCount = CountOccurrences(documentContent, term);
            if (termCount > 0)
            {
                float tfIdf = (float)termCount / documentContent.Length;
                if (termFrequencies.ContainsKey(term))
                {
                    float idf = Mathf.Log((float)documentDatabase.Count / termFrequencies[term]);
                    tfIdf *= idf;
                }
                score += tfIdf;
            }
            
            if (document.title.ToLower().Contains(term))
            {
                score += 0.5f;
            }
            
            if (document.tags.Any(tag => tag.Contains(term)))
            {
                score += 0.3f;
            }
        }
        
        return score;
    }
    
    private int CountOccurrences(string text, string term)
    {
        int count = 0;
        int index = 0;
        
        while ((index = text.IndexOf(term, index)) != -1)
        {
            count++;
            index += term.Length;
        }
        
        return count;
    }
    
    public void AddDocument(Document document)
    {
        documentDatabase.Add(document);
        BuildTermFrequencyIndex();
    }
    
    public void RemoveDocument(string documentId)
    {
        documentDatabase.RemoveAll(doc => doc.id == documentId);
        BuildTermFrequencyIndex();
    }
    
    public Document GetDocumentById(string documentId)
    {
        return documentDatabase.FirstOrDefault(doc => doc.id == documentId);
    }
    
    public List<Document> GetAllDocuments()
    {
        return new List<Document>(documentDatabase);
    }
}