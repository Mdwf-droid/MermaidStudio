# Architecture MermaidStudio

## Vision

MermaidStudio est un éditeur graphique desktop permettant de concevoir des diagrammes Mermaid visuellement, puis de générer un texte Mermaid fidèle.

Le **modèle interne est la source de vérité**. L'UI ne fait qu'éditer ce modèle, et l'export Mermaid est une projection du modèle.

## Couches

### 1. Domain
Contient les entités métier, les invariants, les styles, les règles de validation et les exports Mermaid.

Contraintes :
- aucune dépendance UI
- pas d'effets de bord
- objets cohérents par construction

### 2. Application
Contient les cas d'usage et l'orchestration :
- création/suppression de nœuds
- création/suppression de liens
- changement d'outil actif
- commandes undo/redo
- validation des actions utilisateur

### 3. UI.Avalonia
Contient :
- shell Avalonia
- canvas visuel
- contrôles de rendu (NodeView, EdgeView, PortView)
- toolbar et panneaux latéraux

Règle : l'UI ne prend aucune décision métier. Elle demande à l'application d'exécuter une intention.

## Invariants majeurs

- chaque `Node` a un `Id` stable
- chaque `Port` appartient à un `Node`
- chaque `Edge` relie exactement deux `Port`
- aucun `Edge` invalide ne peut être créé
- un geste utilisateur = une intention explicite = une commande

## Interaction

Les interactions ambiguës sont interdites.

À terme :
- outil Create Node
- outil Select
- outil Link
- outil Edit Label
- outil Pan/Zoom

## Mermaid

Les familles de diagrammes Mermaid seront prises en charge progressivement :
1. Flowchart
2. State
3. Sequence
4. ER
5. C4 / autres familles structurées

Chaque famille dispose à terme :
- d'un modèle de validation
- d'un exporter dédié
- éventuellement d'une UI spécialisée
