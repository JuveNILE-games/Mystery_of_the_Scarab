Getting Started with PurrNet: Your First Multiplayer Scene

Introduction: Your Journey into Multiplayer Begins

Welcome to your first step into the world of multiplayer game development. Building a game where players can interact in a shared world might seem complex, but with the right tools and foundational knowledge, it's an incredibly rewarding process.

This document's purpose is to demystify the three most fundamental components of any PurrNet project: the Network Manager, the Network Transform, and Network Rules. By understanding these three building blocks, you will have a solid foundation for all your future multiplayer projects.

The core philosophy of PurrNet is to embrace and work with the natural workflow of Unity, not against it. This makes the transition to multiplayer development feel surprisingly intuitive. The reason this is possible is architectural: unlike other frameworks that use a game-object-based identity model, PurrNet uses a component-based model where every NetworkBehavior acts as its own identity. This simple but powerful choice is what unlocks much of the freedom and ease-of-use you'll soon experience.


--------------------------------------------------------------------------------


1. The Three Pillars of a PurrNet Scene

Every multiplayer experience you build with PurrNet rests on three foundational "pillars." These components work in harmony to create a seamless, synchronized world for all your players.

To understand their relationship, think of them as the key roles in a stage play: the Director (Network Manager), the Actor's Stage Directions (Network Transform), and the Rules of the Play (Network Rules).

* Network Manager: The central controller that directs the network session, connects players, and introduces objects into the world.
* Network Transform: A component that synchronizes an object's position, rotation, and scale across the network for all players.
* Network Rules: A set of permissions that defines who can perform key actions, like creating objects or calling network functions.

Let's begin by taking a closer look at the first and most important component: the scene's Director, the Network Manager.


--------------------------------------------------------------------------------


2. The Network Manager: Your Scene's Director

The Network Manager is the central brain and controller for your entire multiplayer session. It is the first component you set up in your scene, and it orchestrates everything that happens on the network.

Key Responsibilities of the Network Manager

* Connecting Players: The manager handles the technical side of connecting players. It uses a "Transport Layer" (like the default UDP Transport) to send and receive data. Think of the transport as your game's postal service. Just as a real postal service has options like air mail or ground shipping, PurrNet supports different transports (UDP, WebSockets, Steam). For now, the default UDP Transport is all you need to worry about, as it's the standard for fast-paced games.
* Spawning Player Characters: The manager uses a Player Spawner component to automatically create a character prefab for each player who joins the game. This ensures everyone has a physical presence in the world the moment they connect.
* Managing Networked Objects: Inside the Network Manager is the Network Prefabs list. This is the Director's "cast list." An actor (prefab) can't appear on stage if their name isn't on this list. Only prefabs registered here can be correctly created and synchronized across the network.
* Pro Tip: Forgetting to add a prefab to this list is the single most common cause of spawning errors for new developers. If your object isn't appearing for other players, check this list first!

Now that the Director knows how to bring actors onto the stage, the next step is to ensure their movements are seen by the whole audience.


--------------------------------------------------------------------------------


3. The Network Transform: Synchronizing Movement

The Network Transform component has one simple but vital job: to synchronize an object's position, rotation, and scale across the network. These are the Actor's Stage Directions, ensuring that when an actor moves to stage left, the entire audience sees them move to stage left at the same time.

You can think of it as a live GPS tracker on an object, constantly broadcasting its coordinates so everyone sees it in the same place. This component is placed on the prefabs that need to be synchronized, such as your player character.

A critical helper component that works with it is the Network Ownership Toggle. Its function is simple: it enables or disables other components (like your Player Movement scripts) based on who owns the object. This is essential because it prevents a player from accidentally controlling every other player's character. Only the owner gets to move their own character.

This is the core concept that enforces player agency. Internalizing how ownership works is key to moving beyond simple prototypes.

Now that we know what objects do (move), it's time to define the rules that govern their actions.


--------------------------------------------------------------------------------


4. Network Rules: Defining "Who Can Do What"

Network Rules are a powerful and unique PurrNet feature that defines permissions in your game. The core concept is simple: these rules determine who has the authority to perform key actions, such as creating (spawning) objects or calling special network functions.

For a beginner, there are two fundamental approaches to know:

Rule Philosophy	Description & Analogy	Best For...
Server Authority (Server Auth)	The secure, traditional approach. Only the server can perform critical actions. (Analogy: A strict referee in a competitive match).	Competitive games where preventing cheating is a top priority.
Client Authority (Everyone)	The fast, flexible approach. It allows clients to perform more actions themselves. (Analogy: A casual co-op game among friends).	Prototyping, co-op games, and getting started quickly because it mirrors single-player Unity development.

For beginners, the Everyone rules (often referred to as Unsafe Rules) are highly recommended. They simplify the development process significantly because they allow you to write code that feels just like it would for a single-player game. For example, with Everyone rules, a client can spawn a networked object by simply calling Instantiate() and despawn it with Destroy(), exactly as you would in standard Unity development.

Let's tie all three of these concepts together to see the complete picture in action.


--------------------------------------------------------------------------------


5. Putting It All Together: A Simple Story

To see how these three pillars work in harmony, let's walk through the lifecycle of a simple multiplayer session.

1. The Host Begins: The Host presses play. Their Network Manager, acting as the "Server," immediately starts the show. It consults its Player Spawner, finds the designated player prefab, and creates an instance of it in the scene. The Host can now move their character around in the world.
2. A Client Joins: A Client connects to the Host's game. The Server's Network Manager receives the connection request and, like a diligent Director, brings a new actor on stage. It once again uses the Player Spawner to create a new instance of the player prefab, granting ownership of this new character to the Client who just joined.
3. A Player Moves: The Client, now in the game, pushes their forward key. On their machine, the character moves instantly. The Network Transform component on their character, acting like a vigilant GPS tracker, immediately detects this position change and sends a tiny data packet to the Host (the Server), saying, "I've moved to these new coordinates!"
4. The Scene Synchronizes: The Server receives this data and relays it to all other players (in this case, just the Host). The Host's game updates the Client's character position on their screen. Now, everyone sees the same movement, perfectly synchronized.
5. The Rules Make it Easy: This entire flow happens seamlessly because the Network Rules were set to Everyone. The client could move and have that movement broadcast without needing complex, server-only code, allowing for a direct and simple workflow for this basic action.

This simple story illustrates the core loop of a multiplayer session in PurrNet.


--------------------------------------------------------------------------------


Conclusion: Your Foundation is Set

Congratulations! You now understand the three fundamental pillars of multiplayer development with PurrNet.

By grasping the roles of the Network Manager (the director), the Network Transform (the synchronizer), and Network Rules (the permissions), you have the foundational knowledge to start building and experimenting with your own multiplayer games. This is the foundation upon which all more complex systems are built.

If you have questions or need help along the way, the PurrNet Discord is the main place for support. Welcome to the community, and happy developing!
