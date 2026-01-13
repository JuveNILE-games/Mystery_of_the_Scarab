Design Document: A Scalable Multiplayer Architecture with PurrNet in Unity

1.0 Introduction

This document presents a robust, scalable, and maintainable architectural design for multiplayer games developed in Unity using the PurrNet networking library. The proposed design prioritizes modularity and decoupling through the strategic use of C# interfaces, abstraction, and PurrNet's advanced networking features. By establishing clear contracts between systems rather than creating hard dependencies, this architecture allows for the addition of new features and entities with minimal impact on existing code. This document is intended for experienced developers aiming to build extensible, maintainable, and cheat-resistant networked systems from the ground up.

The core design goals guiding this architecture are as follows:

* Scalability: The ability to add new entities (players, enemies, items) and interactions without requiring extensive rewrites of existing systems.
* Maintainability: The ease with which systems can be modified, debugged, and updated over the project's lifecycle.
* Modularity: The practice of building self-contained, interchangeable components that reduce hard dependencies across the codebase.

The foundation of this architecture lies in a clear philosophy of decoupling, which is essential for managing the complexity inherent in multiplayer game development.

2.0 Core Architectural Philosophy: Decoupling with Interfaces and Abstraction

A common pitfall in multiplayer game development is the creation of a tangled "net" of hard dependencies, where systems are tightly coupled. For example, a player's attack script might contain explicit checks for an EnemyHealth component, a BossHealth component, and a DestructibleCrate component. This approach is brittle; adding a new type of damageable object requires modifying the player's attack logic, leading to code that is difficult to scale and maintain. This section outlines a strategy to eliminate these hard dependencies through interfaces and abstraction.

The Power of Interfaces for Decoupling

The most effective solution to hard dependencies is to define contracts using C# interfaces. An interface establishes a set of methods and properties that a class must implement, without dictating how it should implement them. For a system involving combat or interaction, an IDamageable interface is an ideal starting point.

It is critical to distinguish between the C# contract and its networked implementation. The interface itself is a standard language feature; it is the implementation of this interface within a NetworkBehavior that will leverage PurrNet's RPCs to facilitate rich, contextual communication.

public interface IDamageable
{
    // Defines a contract for any object that can be "hit".
    // It is generic to accept the attacker's type and returns a Task
    // to allow for asynchronous, server-authoritative feedback.
    Task<DamageResult> OnHit<T>(T attacker, int damage);
}


By using this interface, any system that deals damage—be it a player's weapon, an environmental trap, or an area-of-effect spell—no longer needs to know about the specific implementation of what it is hitting. It only needs to find a component that implements IDamageable and call the OnHit method. This completely decouples the "damager" from the "damageable," allowing for true modularity. An Enemy, a Boss, and a DestructibleCrate can all implement this interface in their own unique ways, and the attacking system will interact with all of them through the exact same code path.

The Role of Abstraction for Shared Logic

While interfaces define a contract, abstract base classes provide a powerful way to share common functionality among a family of related objects. An abstract class can implement an interface and provide a foundational implementation that its derived classes can extend or override. Following our example, a BaseHealth abstract class can implement IDamageable.

The BaseHealth class becomes the ideal location for shared logic and, critically, for centralized networking code. Instead of duplicating SyncVars for health and RPCs for damage application across EnemyHealth, BossHealth, and other similar scripts, all of this logic can be consolidated into the BaseHealth class. This adheres to the "Don't Repeat Yourself" (DRY) principle and ensures that the core networking logic for health and damage has a single, authoritative source.

However, to implement this pattern effectively, we must first ground our design in the foundational components and authority model that PurrNet provides.

3.0 Foundational PurrNet Concepts

To effectively implement the proposed architecture, a firm grasp of PurrNet's core components and its authority model is required. These features provide the underlying mechanics that make a decoupled and server-authoritative design possible.

3.1 Component-Based Identity

A key architectural distinction of PurrNet compared to other solutions like Mirror or FishNet is its use of component-based identities. In PurrNet, every NetworkBehavior acts as its own independent network identity, rather than having a single NetworkIdentity or NetworkObject that represents the entire GameObject.

This approach offers significant benefits:

* Multiple Identities: A single GameObject can host multiple NetworkBehavior components, each with its own unique network ID and ownership. This allows for complex entities where different sub-components can be controlled by different players or systems.
* Flexible Prefab Nesting: The component-based model removes restrictions on prefab hierarchies. Networked prefabs can be nested within each other without issue, as each NetworkBehavior stands on its own.
* Modularity: Each component is treated as a self-contained networked entity, which aligns perfectly with a modular design philosophy.

This component-centric approach is not just a technical detail; it is the bedrock of our modularity goal, allowing us to compose complex networked entities from independent, reusable behaviors without the rigid constraints of a single, monolithic Network Object.

3.2 The Network Manager

The NetworkManager is the central hub for the entire network session. It is a MonoBehaviour component placed in the scene that manages the connection lifecycle, hosts the selected transport layer (e.g., UDP Transport), and maintains the list of spawnable NetworkPrefabs. Its configuration dictates how clients connect and how the game session is initiated.

3.3 The Network Rules & Authority Model

PurrNet's authority model is managed through Network Rules. These rules define who is allowed to perform critical network actions, such as spawning objects or calling RPCs. The two primary models are:

* Server Auth: Only the server is permitted to perform authoritative actions. Clients must request actions from the server via ServerRPCs. This model provides a secure, cheat-resistant environment and is the standard for most production games.
* "Everyone": Both the server and clients are allowed to perform network actions directly. This is extremely useful for rapid prototyping and co-op games where cheating is not a major concern, but it is inherently insecure for competitive environments.

For the purposes of building a robust and secure game, this design document will proceed with a server-authoritative model as the best practice.

With these foundational concepts in mind, we can now explore a detailed implementation of the damage system.

4.0 Case Study: Implementing a Modular Damage System

This section provides a concrete implementation of the architectural philosophy discussed earlier, demonstrating how to build the IDamageable system from the ground up using PurrNet's features.

4.1 The Base Health Implementation

The BaseHealth abstract class serves as the foundation for any entity that has a health pool. It inherits from NetworkBehavior, implements the IDamageable interface, and encapsulates all the core networking logic.

using PurrNet;
using System.Threading.Tasks;
using UnityEngine;

// An abstract base class that centralizes health logic and networking.
public abstract class BaseHealth : NetworkBehavior, IDamageable
{
    [SerializeField] private int _maxHealth = 100;
    
    // A SyncVar synchronizes the health value from the server to all clients.
    protected SyncVar<int> _currentHealth;

    // OnInitializedModules is called before the object is spawned on the network,
    // making it a safe place to initialize network state.
    public override void OnInitializedModules()
    {
        _currentHealth = new SyncVar<int>(_maxHealth);
    }
    
    // This ServerRPC is the single, authoritative entry point for all damage.
    // Clients call this method, and it executes exclusively on the server.
    [ServerRPC]
    public async Task<DamageResult> OnHit<T>(T attacker, int damage)
    {
        // Server-side logic can be added here, e.g., checking for blocks.
        // For this example, we proceed directly to dealing damage.
        Damage(damage);
        return DamageResult.Dealt; // Return feedback to the calling client.
    }

    // A protected virtual method for the core health reduction logic.
    protected virtual void Damage(int damage)
    {
        if (_currentHealth.Value <= 0) return;

        _currentHealth.Value -= damage;

        if (_currentHealth.Value <= 0)
        {
            Die();
        }
    }

    // An abstract method that forces derived classes to implement
    // their own unique death behaviors (e.g., animations, loot drops).
    protected abstract void Die();
}


This implementation centralizes all networking logic, establishing a single source of truth for damage processing and state synchronization. It prevents state divergence by ensuring only the server can modify _currentHealth.Value. It centralizes security, as the [ServerRPC] acts as the single, validated point of entry for all damage requests. Finally, it promotes code reuse, as any class that inherits from BaseHealth receives this robust networking functionality "for free" without any code duplication.

4.2 Concrete Damageable Implementations

With the base class established, creating specific implementations is simple and clean.

First, an EnemyHealth class can inherit from BaseHealth to manage a standard enemy. Its primary responsibility is to implement a unique death behavior. To ensure all clients witness the death, the Die() method triggers an [ObserverRPC], which is an RPC executed on all clients.

using PurrNet;

public class EnemyHealth : BaseHealth
{
    // Override the abstract Die method with a concrete implementation.
    protected override void Die()
    {
        // This RPC is called on the server (from the base class)
        // and executes on all clients to show the death animation.
        PlayDeathAnimationRPC();
    }

    [ObserverRPC]
    protected void PlayDeathAnimationRPC()
    {
        // Logic to play death animation and disable the enemy.
    }
}


Second, to demonstrate the flexibility of the interface, a DestructibleCrate class can implement IDamageable directly without inheriting from BaseHealth. This is suitable for objects that can be destroyed but do not have a traditional health pool.

public class DestructibleCrate : NetworkBehavior, IDamageable
{
    [SerializeField] private int _hitsToBreak = 3;
    private SyncVar<int> _currentHits;

    public override void OnInitializedModules()
    {
        _currentHits = new SyncVar<int>(0);
    }

    [ServerRPC]
    public async Task<DamageResult> OnHit<T>(T attacker, int damage)
    {
        _currentHits.Value++;
        if (_currentHits.Value >= _hitsToBreak)
        {
            DestroySelfRPC(); // An ObserverRPC to handle destruction visuals
            return DamageResult.Dealt;
        }
        return DamageResult.Dealt;
    }

    [ObserverRPC]
    private void DestroySelfRPC()
    {
        // Logic to play breaking effect and destroy the object.
    }
}


While this system is now fully functional, PurrNet's advanced RPC features can make it even more powerful and flexible, enabling richer client-server communication.

5.0 Leveraging Advanced RPCs for Dynamic Communication

PurrNet's advanced RPC features enable communication that goes beyond simple fire-and-forget commands. By supporting generics and awaitable return values, they allow for a more dynamic and responsive-feeling game, which is critical for a modern multiplayer experience.

5.1 Providing Context with Generic RPCs

The OnHit<T>(T attacker, int damage) method signature is a prime example of a generic RPC. By making the [ServerRPC] generic, the client can pass any serializable type to the server. The most powerful use case within this architecture is passing a direct reference to another NetworkBehavior (the attacker).

This provides the server with complete, authoritative context about who initiated the action without the client needing to manually serialize an entity ID or other complex data into a packet. The server can then directly access the attacker's components to apply logic, such as checking for faction alignment or retrieving weapon stats. This simplifies client-side code and provides the server with rich, authoritative information.

5.2 Receiving Feedback with Awaitable (Returnable) RPCs

PurrNet RPCs can be made awaitable by having them return a Task<T>. In our case study, the OnHit RPC returns a Task<DamageResult>, allowing the server to send a result back to the specific client that made the call.

This feature allows the server to perform complex, authoritative logic and then inform the client of the outcome. For instance, the server could calculate damage modifiers, check if the attack was blocked by armor, or determine if the target was immune. It would then return a DamageResult enum (Dealt, Blocked, Immune, etc.).

The impact on game feel is significant. The client-side code can await the RPC call and, upon receiving the DamageResult, trigger the appropriate feedback.

* If the result is Dealt, it can play a satisfying hit sound and impact particles.
* If the result is Blocked, it can play a metallic "clank" sound and a shield particle effect.

This creates a highly responsive experience where the client's feedback is directly tied to the server's authoritative game state, bridging the gap between player action and server-validated outcome.

Furthermore, these advanced RPC features are fully composable, allowing for highly specific communication patterns such as a single [ServerRPC] that is simultaneously static, generic, and awaitable.

6.0 Advanced Encapsulation with Network Modules

While using an abstract base class like BaseHealth is a valid and effective pattern, PurrNet offers a more advanced and composable alternative for encapsulating reusable logic: Network Modules.

A NetworkModule is a serializable C# class (not a MonoBehaviour) that inherits from PurrNet.NetworkModule. It can contain its own SyncVars, RPCs, and networked Actions, effectively acting as a self-contained package of networked functionality that can be added to any NetworkBehavior.

We can refactor our BaseHealth logic into a HealthModule. This shifts the design from an inheritance-based model (a class is a type of health component) to a composition-based model (a class has a health module).

Feature	BaseHealth (Inheritance)	HealthModule (Composition)
Structure	An abstract class inheriting NetworkBehavior.	A [Serializable] class inheriting NetworkModule.
Usage	Classes like EnemyHealth inherit from it.	Any NetworkBehavior can contain it as a field.
Flexibility	Limited by single inheritance in C#.	Highly flexible; any object can have a HealthModule, even if it already inherits from another class.
Reusability	Reusable for any object that fits the inheritance chain.	Reusable across any NetworkBehavior via composition.

For any system intended to be a cross-cutting concern (e.g., health, mana, inventory, status effects), composition via NetworkModule is the superior and prescribed pattern in this architecture. It definitively breaks the limitations of single inheritance and maximizes combinatorial possibilities for complex entities.

While Network Modules perfect the encapsulation of entity-level logic, a robust architecture must also systematically manage the high-level game loop. This is where we shift our focus from individual components to the orchestration of the overall game flow.

7.0 Managing Game Flow with the Network State Machine

Managing the high-level game loop (e.g., lobby, pre-round, round in progress, post-round) is a critical challenge in multiplayer games. PurrNet provides a purpose-built solution for this: the Auto Network State Machine.

This system is built around two core components:

* The StateMachine: A NetworkBehavior placed in the scene that manages an ordered list of states.
* StateNode: A script, also a NetworkBehavior, that represents a single state in the game loop (e.g., WarmupState, RoundInProgressState, RoundEndState).

Each StateNode has key lifecycle methods that can be overridden to implement state-specific logic:

* OnEnter(): Called when the state becomes active.
* OnStateUpdate(): Called every frame while the state is active (similar to Update()).
* OnExit(): Called when the state is being deactivated.

The primary benefit of this system is that the StateMachine automatically synchronizes the current state from the server to all clients. This includes players who connect late; they will immediately be brought into the correct game state upon joining, ensuring a consistent experience for everyone.

Furthermore, states can be generic (StateNode<T>), allowing the server to pass authoritative data when transitioning. For example, when moving from RoundInProgressState to RoundEndState, the server can pass a RoundResult object containing information about the winning team and player scores. This ensures that all clients receive the final, authoritative outcome of the round simultaneously.

8.0 Conclusion and Best Practices Summary

A scalable, maintainable multiplayer game is not built by accident; it is the result of a deliberate architectural design founded on loosely-coupled, modular components. By leveraging C# interfaces to define contracts, abstract classes and Network Modules to encapsulate logic, and PurrNet's powerful, feature-rich networking layer, developers can create systems that are resilient to change and easy to extend. This approach moves away from a tangled web of dependencies toward a clean, organized structure where components interact through well-defined, networked contracts.

The key design principles and best practices discussed throughout this document are summarized below:

1. Define Contracts, Compose Behaviors: Use interfaces (IDamageable) to establish contracts and encapsulate reusable logic within NetworkModules (HealthModule), strongly favoring composition over inheritance to maximize modularity.
2. Centralize Network Logic: Place RPCs and state synchronization variables (SyncVar) in base classes (BaseHealth) or modules (HealthModule) to avoid code duplication and ensure a single source of truth for networked behaviors.
3. Adopt a Server-Authoritative Model: Use [ServerRPC] for all gameplay-critical actions to create a secure and cheat-resistant environment, making the server the ultimate arbiter of the game state.
4. Leverage Advanced RPCs: Utilize generic RPCs to pass rich context (like an attacker's identity) and awaitable RPCs to provide authoritative feedback to clients, significantly enhancing game feel and responsiveness.
5. Manage Game Flow Systematically: Use the Auto Network State Machine to orchestrate the high-level game loop, ensuring robust and automatic state synchronization for all players, including those who join mid-game.
