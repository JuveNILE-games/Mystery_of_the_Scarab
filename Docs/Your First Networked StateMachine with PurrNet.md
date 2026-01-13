Your First Networked State Machine in PurrNet: A Step-by-Step Guide

Introduction: What is a State Machine and Why Use It?

Welcome! If you're building a multiplayer game, you'll quickly realize you need a way to manage different phases or stages of gameplay. This is where a state machine comes in. Think of it as a manager that controls whether your game is in a warm-up phase, a round-in-progress, a buy phase, or a game-over screen. It ensures that every part of your game logic knows exactly what "state" the game is currently in.

The real power of PurrNet's StateMachine component is that it automatically handles synchronizing these states across all connected players. When the server decides to move from the "Warm-up" state to the "Round Start" state, every client follows suit automatically. This guide will walk you through creating a simple, three-state machine to teach you the fundamentals of this powerful system.


--------------------------------------------------------------------------------


1. The Foundation: Setting Up the StateMachine Component

Let's start by setting up the central manager for our states in the Unity Editor.

1. Create the Manager: In your Unity scene, create a new empty GameObject. A clear name like GameStateMachine is perfect.
2. Add the Component: With the GameStateMachine GameObject selected, click "Add Component" in the Inspector and search for StateMachine. Add the one from PurrNet (it has a green little dot).
3. 
4. The Component's Role: This component serves two primary functions:
  * State Management: It holds a list of all possible states your game can be in. You will drag your state objects into this list.
  * Debugging: During runtime, it provides useful information in the Inspector, showing you the current, previous, and next states, which is incredibly helpful for troubleshooting.

💡 Teacher's Tip Organizing core game logic, like state machines or network managers, onto their own dedicated GameObjects is an excellent practice. It promotes the Single Responsibility Principle, making systems like the state machine self-contained and much easier to debug or even replace later on.

Now that our manager is ready, it's time to create our very first state.


--------------------------------------------------------------------------------


2. Creating Your First State: The StateNode

Every state in our machine will be its own C# script that inherits from a special class called StateNode.

1. The StateNode Class: The StateNode is the building block for all states. It inherits from NetworkBehavior. Inheriting from NetworkBehavior is what gives our states their networking powers. It's the base class for all networked components in PurrNet, providing access to properties like IsServer and the ability to define RPCs.
2. Create the Script: Create a new C# script in your project. Name it StateOne.
3. Code Structure: Open the script and set it up to inherit from StateNode.
4. Core Methods: A StateNode gives you three fundamental methods you can override to control its behavior.

Method	Purpose
public override void OnEnter()	Runs once when the machine enters this state. Perfect for setup logic.
public override void OnExit()	Runs once when the machine leaves this state. Ideal for cleanup.
public override void StateUpdate()	Runs every frame while this is the active state, just like a normal Update() method.

1. Final Code: Let's add some simple logs to our StateOne script so we can see when it becomes active.

With our script ready, let's connect it to our State Machine in the Unity Editor.


--------------------------------------------------------------------------------


3. Adding the State to the Machine

Now we'll link our StateOne.cs script to the StateMachine component.

1. Create the State GameObject: Create a new empty GameObject as a child of GameStateMachine. Name it State One. This structure is powerful because it allows each state to hold its own unique components, child objects, or configuration data directly on its GameObject, which can be enabled or disabled in OnEnter() and OnExit().
2. Attach the Script: Select the State One GameObject and add your StateOne.cs script to it as a component.
3. Populate the List: Select the parent GameStateMachine GameObject. In the Inspector, you'll see the StateMachine component with an empty list called States. Drag the State One child GameObject from the Hierarchy into this list.
4. 

⚠️ Common Pitfall Forgetting to drag a state's GameObject into the States list is a common error. If your state transitions aren't working, this is the first place to check! The StateMachine can only transition to states it knows about from this list.

Our machine now knows about 'State One'. Next, we'll write the code to control when we move from one state to another.


--------------------------------------------------------------------------------


4. Controlling the Flow: How to Switch States

A critical rule for networked state machines is that only the server should control state changes. This ensures that all clients stay perfectly synchronized.

1. The machine Variable: Every StateNode has a built-in reference called machine. This variable allows it to communicate with its parent StateMachine and tell it when to change states.
2. Implement the Logic: Let's add code to StateOne.cs so that pressing the "X" key on the server tells the machine to move to the next state. The IsServer property is a boolean we get from inheriting NetworkBehavior, and it's only true on the server or host instance of the game.
3. State Transition Methods: There are three main ways to change the state:
  * machine.Next(): Moves to the next state in the States list. This is the most common method for sequential game phases (e.g., Warmup -> Round Start -> Round End).
  * machine.Prev(): Moves to the previous state in the list.
  * machine.SetState(targetState): Jumps directly to a specific state in the list. This is extremely useful for creating loops. For example, after a 'Round End' state, you could use machine.SetState() to jump back to the 'Round Start' state, creating a loop. This is more robust than using machine.Prev() because it doesn't depend on the list order for non-sequential logic.

Now that we have the logic for switching states, let's expand our machine and see it in action.


--------------------------------------------------------------------------------


5. Passing Data Between States

First, create two new scripts, StateTwo.cs and StateThree.cs, just like you did for StateOne. Create their corresponding GameObjects in the scene as children of GameStateMachine and add them to the States list in the correct order: State One, State Two, State Three.

Now, what if you need to pass information from one state to the next, like a round score or a specific configuration? This is solved using a generic StateNode<T>.

1. Generic StateNode<T>: This version of StateNode allows you to specify a data type (T) that must be passed into it when it's entered. T can be any type that Unity knows how to send over the network, such as primitive types like int and string, Unity structs like Vector3, or your own custom classes/structs marked with [System.Serializable].
2. Modify StateThree: Let's change StateThree to expect an integer when it's entered.
3. This failsafe is your best friend during development. If you ever accidentally transition to this state without data, this error message will immediately tell you what went wrong and where.
4. Modify StateTwo: Now, we'll update StateTwo to pass the integer 420 when it transitions to the next state (StateThree).

Our machine is now fully functional and can even pass data between states. Let's look at one of PurrNet's most powerful features related to this system: automatic buffering.


--------------------------------------------------------------------------------


6. The Magic of PurrNet: Automatic Buffering and Late-Joining

What happens when a new player joins a game that is already in progress? With PurrNet's state machine, the system automatically "catches them up." This is called buffering. During play, the StateMachine component provides excellent debug information right in the inspector.



Let's run a test to see this automatic buffering in action:

1. Start a host/server and a client in the Unity Editor.
2. On the server, press 'X' twice to progress the state machine through StateOne and StateTwo until it reaches StateThree. You should see the log "Entered State Three with data: 420" on both the server and the client.
3. Now, stop the client application.
4. Restart the client so it reconnects to the server.

You will see that the reconnected client immediately enters StateThree, and its console will log "Entered State Three with data: 420".

This is a critical feature for robust multiplayer games. You don't need to write any extra code to handle late-joining players. PurrNet's state machine automatically buffers the current state and its associated data, ensuring every player is always in the correct phase of the game. This 'it just works' approach to late-joining is a perfect example of PurrNet's design philosophy: embracing Unity's natural workflow to handle complex networking scenarios for you, so you can focus on your game's logic.


--------------------------------------------------------------------------------


7. Conclusion and Next Steps

Congratulations! You have successfully built a networked state machine and learned some of its most powerful features.

You now have the skills to:

1. Set up the StateMachine component.
2. Create custom states using StateNode.
3. Control the game flow using machine.Next() and machine.SetState().
4. Pass data between states with the generic StateNode<T>.
5. Understand the power of automatic state buffering for late-joining players.

As a next step, try building on what you've learned. Create a simple round-based game loop where the final state uses machine.SetState() to transition back to the first "Round Start" state, creating a complete, repeatable cycle for your game.
