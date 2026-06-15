---
trigger: model_decision
description: C# Code
---

# Workspace Constraints: C# Desktop Development

## Environment Detection & Frameworks
- For existing applications: Scan the codebase or project files first to check whether the UI framework is WinForms or WPF, then match that framework exactly.
- For new applications: Always bootstrap using Windows Presentation Foundation (WPF) with modern MVVM architecture.

## Programming Paradigm
- Write strictly structured Object-Oriented Programming (OOP) code featuring encapsulation, strong typing, and clear class separation, unless functional data manipulation is explicitly requested.

## Language & Compiler Version
- Target Version: C# 14 running on .NET 10.
- Prefer modern language features: Use primary constructors, the `field` keyword for properties, collection expressions `[]`, and file-scoped namespaces.

## Data & Serialization Preference
- For all JSON manipulation, strictly use `System.Text.Json` source generators (`JsonSerializerContext`). 
- Do not instantiate `JsonSerializerOptions` inline; use cached static instances or generated contexts to maximize .NET 10 JIT optimization.
