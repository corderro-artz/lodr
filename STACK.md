# LODR — Platform Architecture Specification

A minimal, technology‑layer specification defining the foundational architecture for a local‑first, installable PWA used to visualize pallet layouts inside truck trailers.  
This document intentionally excludes feature definitions and focuses solely on platform layers and their assigned technologies.

---

## 1. Application Classification

- **Type:** Progressive Web App (PWA)  
- **Mode:** Local‑first, offline‑capable  
- **Runtime:** 100% client‑side  
- **Backend:** None (no servers, no APIs)  
- **Target Devices:** Desktop + tablet  
- **Interaction Model:** Single‑page, no scrolling, paginated/tabbed navigation  

---

## 2. Layered Architecture Overview

┌──────────────────────────────┐  
│        PWA Shell Layer        │  ← Manifest + Service Worker  
├──────────────────────────────┤  
│     UI / Presentation Layer   │  ← Blazor WASM + TailwindCSS  
├──────────────────────────────┤  
│ Visualization Engine Layer    │  ← PixiJS (2D) or Three.js (3D)  
├──────────────────────────────┤  
│   Application Logic Layer     │  ← C# / .NET (pure logic)  
├──────────────────────────────┤  
│   Local Storage / Persistence │  ← IndexedDB  
└──────────────────────────────┘  

Each layer is isolated, deterministic, and technology‑bound.

---

## 3. Technology Assignment by Layer

### 3.1 PWA Shell Layer

**Technologies**

- PWA Manifest (JSON)  
- Service Worker (JavaScript)  

**Responsibilities**

- Installability  
- Offline caching of all assets  
- App shell lifecycle management  
- Versioned static asset delivery  

---

### 3.2 UI / Presentation Layer

**Technologies**

- Blazor WebAssembly (AOT)  
- TailwindCSS  

**Responsibilities**

- Component rendering  
- Page/step/tab navigation  
- Layout and styling (no scrollbars)  
- User input handling  
- State binding  

**Constraints**

- No business logic in UI components  
- No direct visualization logic in UI components  

---

### 3.3 Visualization Engine Layer

**Technologies**

- PixiJS (default 2D mode)  
  or  
- Three.js (optional 3D mode)  

**Responsibilities**

- Rendering truck interior  
- Rendering pallet placements  
- Camera/zoom/pan/orbit controls  
- High‑performance draw loop  

**Constraints**

- Accessed only via Blazor → JS interop  
- Visualization logic stays in JavaScript  
- Calculation logic stays in C#  

---

### 3.4 Application Logic Layer

**Technologies**

- C# / .NET (Blazor WASM)  

**Responsibilities**

- Dimension handling  
- Pallet fit calculations  
- Orientation rules  
- Grid/coordinate generation  
- Validation and transformation  

**Constraints**

- Pure functions only  
- No UI references  
- No JS references  
- No side effects  

---

### 3.5 Local Storage / Persistence Layer

**Technologies**

- IndexedDB (via Blazor.LocalStorage or custom JS interop)  

**Responsibilities**

- Saving user configurations  
- Loading presets  
- Storing last‑used settings  
- Offline persistence  

**Constraints**

- All data stored locally  
- No remote sync or network calls  

---

## 4. Cross‑Cutting Requirements

### 4.1 Performance

- AOT‑compiled .NET for logic  
- WebGL acceleration for visualization  
- TailwindCSS for minimal CSS footprint  
- No blocking operations on UI thread  

### 4.2 Modularity

- Each layer is replaceable  
- No circular dependencies  
- Visualization engine can be swapped (2D ↔ 3D)  

### 4.3 Extensibility

- New features added as isolated modules  
- Feature‑branch workflow supported by strict layer boundaries  

---

## 5. Agent Development Rules

### 5.1 Agents MUST

- Place UI code in the UI layer  
- Place math/logic in the logic layer  
- Place rendering code in the visualization layer  
- Use IndexedDB for persistence  
- Keep layers isolated and deterministic  

### 5.2 Agents MUST NOT

- Mix UI and logic  
- Put calculations in JavaScript  
- Put rendering in C#  
- Add backend dependencies  
- Introduce scrolling or scrollbars  
- Add libraries >200 KB without explicit approval  

---

## 6. Summary Table

| Layer         | Technology                   | Purpose                      |
|--------------|------------------------------|------------------------------|
| PWA Shell    | Manifest + Service Worker    | Installable, offline app     |
| UI Layer     | Blazor WASM + TailwindCSS    | Layout, navigation, input    |
| Visualization| PixiJS or Three.js           | Render pallets + truck       |
| Logic Layer  | C# / .NET                    | All calculations + rules     |
| Persistence  | IndexedDB                    | Local storage                |

---

## 7. Document Purpose

This document defines the **stable architectural foundation** for the application.  
All future features, modules, and enhancements must conform to this layer structure and technology assignment.