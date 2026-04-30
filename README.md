# MermaidStudio — Architecture Scaffold

Ce dépôt contient un **socle d'architecture** pour un futur éditeur graphique Mermaid basé sur **Avalonia 12.0.2**.

> Objectif : fournir une base propre, lisible et durable pour construire l'application sans dette technique inutile.

## Structure

- `docs/` : vision produit, architecture, roadmap, ADRs
- `src/MermaidStudio.Domain/` : modèle métier pur, sans dépendance UI
- `src/MermaidStudio.Application/` : cas d'usage, commandes, état d'édition
- `src/MermaidStudio.UI.Avalonia/` : shell UI Avalonia 12.0.2
- `tests/MermaidStudio.Tests/` : base pour les tests unitaires et d'intégration

## Convention de travail

1. Une phase = un objectif unique
2. Aucune régression tolérée
3. Pas de logique métier dans l'UI
4. Les exporters Mermaid consomment le modèle métier, jamais les contrôles Avalonia
5. Toute fonctionnalité ajoutée doit être testable indépendamment de l'UI

## Contenu du scaffold

- modèles de domaine de base (`DiagramDocument`, `Node`, `Port`, `Edge`)
- types Mermaid ciblés (`DiagramKind`)
- base d'orchestration applicative (`EditorTool`, commandes, état d'édition)
- shell Avalonia minimal (`App`, `Program`, `MainWindow`)
- documentation d'architecture et feuille de route

## Prochaines étapes suggérées

1. Valider/ajuster les invariants métier dans `docs/Architecture.md`
2. Fixer la toute première baseline fonctionnelle (S0/S1)
3. Écrire les tests du domaine avant toute interaction complexe
4. Construire l'éditeur en incréments courts et irréversibles
