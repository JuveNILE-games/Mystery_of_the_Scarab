Advanced RPC Techniques in PurrNet for Modular and Scalable Game Development

1.0 Introduction: Beyond Basic Remote Procedure Calls

While standard Remote Procedure Calls (RPCs) are a fundamental pillar of multiplayer game development, PurrNet's core mission is to embrace the natural workflow of Unity rather than fight it. This philosophy drives the design of our advanced RPC features, which are crafted to elevate network architecture by making networked code feel as intuitive and powerful as modern, idiomatic C#.

This document serves as a detailed technical guide for Unity developers on implementing static, generic, and awaitable RPCs. The strategic importance of these features cannot be overstated. They are not merely additions, but direct results of our design principles, enabling a shift from simple fire-and-forget commands to a highly modular, scalable, and maintainable networking model. By mastering these techniques, you can write cleaner code that aligns with C# best practices and construct powerful, reusable tools that accelerate development.

Before exploring these advanced types, it is essential to have a firm grasp of PurrNet's foundational RPC structure, upon which these powerful features are built.

2.0 Foundational RPCs in PurrNet: A Brief Overview

A solid understanding of PurrNet's core RPC attributes and the context in which they operate is crucial for effectively implementing the advanced techniques discussed later. These attributes are the primary building blocks for communication, defined as methods within a class that inherits from NetworkBehaviour.

RPC Attribute	Function
[ServerRPC]	A call typically made from a client to be executed on the server. This is the standard method for clients to send commands or data to the authoritative server.
[ObserverRPC]	A call typically made from the server to be executed on all clients (observers) of the object, including the server. Ideal for broadcasting state changes or events to all relevant players.
[TargetRPC]	A call typically made from the server to be executed on a specific, targeted client. Useful for sending private information or commands to a single player.

It's important to note that PurrNet's flexible NetworkRules system can override these standard roles. This allows for more permissive, client-authoritative models where clients can call any RPC type if the project's design requires it, offering maximum workflow flexibility.

Architecturally, PurrNet’s NetworkBehaviour is a key differentiator. Unlike systems that tie a single network identity to a GameObject, PurrNet employs a component-based identity system. This means a single GameObject can host multiple NetworkBehaviour components, each acting as its own unique network identity with its own owner. This design provides unparalleled modularity, allowing you to build complex networked entities from smaller, reusable, and independently network-aware components—a paradigm that aligns perfectly with Unity's own component-based philosophy.

We will now build upon this foundation to explore RPCs that offer greater accessibility, reusability, and asynchronous control flow across your entire codebase.

3.0 Static RPCs: Achieving Global Accessibility

In general programming, static methods provide a powerful way to offer global functionality without requiring an instance of an object. PurrNet extends this powerful concept to its networking layer, enabling the creation of universally accessible network endpoints that can be invoked from anywhere in your code.

A Static RPC in PurrNet is simply an RPC attribute applied to a static method within any NetworkBehaviour. The primary advantage of this approach is its remarkable accessibility. As the source material notes, "you can very easily call static rpcs from well anywhere any script could essentially just call" it. This powerful feature decouples the caller from the network object instance, allowing for the creation of centralized, service-oriented network endpoints.

Code Implementation Example

The following example demonstrates how to declare a static ServerRPC and call it from another script.

// Within a NetworkBehaviour script, for example, 'AdvancedRPCs.cs'
[ServerRPC(requireOwnership = false)]
public static void MyStaticRPC(string message)
{
    Debug.Log(message);
}

// This RPC can now be called from any other script in the project
// without needing a reference to an instance of AdvancedRPCs.
void SomeOtherMethod()
{
    string msg = "Hello from a static RPC";
    AdvancedRPCs.MyStaticRPC(msg);
}


Use Cases

Static RPCs are exceptionally well-suited for creating centralized, globally accessible network functions. Practical applications include:

* Centralized Game Managers: Triggering global game state changes (e.g., starting a match, ending a round).
* Global Event Systems: Broadcasting system-wide notifications that aren't tied to a specific game object.
* Utility Services: Implementing cheat detection, analytics, or logging services that need to be triggered from disparate parts of the game logic.

While static RPCs solve the challenge of accessibility, generic RPCs address the need for code reusability and type flexibility.

4.0 Generic RPCs: Building Type-Agnostic, Reusable Network Logic

Generics are a cornerstone of modern C# programming, enabling the creation of highly flexible and reusable components that can operate on any data type. PurrNet seamlessly integrates this principle into its network communication layer. Support for generic RPCs allows developers to write a single network method that can handle various data types, drastically reducing code duplication and increasing the modularity of the network architecture.

A Generic RPC is a method marked with an RPC attribute that uses one or more generic type parameters (e.g., <T>). PurrNet will automatically handle the serialization of the provided data type.

Code Implementation Example

This example illustrates the definition and invocation of a single generic ServerRPC with two different data types: a string and an int.

// Within a NetworkBehaviour script
[ServerRPC(requireOwnership = false)]
private void MyGenericRPC<T>(T data)
{
    Debug.Log($"Received generic data: {data}");
    Debug.Log($"Type: {typeof(T)}");
}

// Calling the generic RPC with different types
void Update()
{
    if (Input.GetKeyDown(KeyCode.X))
    {
        // Call with a string
        MyGenericRPC("Hello World");
    }
    if (Input.GetKeyDown(KeyCode.C))
    {
        // Call with an integer
        MyGenericRPC(420);
    }
}


Impact on Scalability

The ability to create a single RPC endpoint that can process multiple data types is incredibly powerful for building scalable systems. This integration is described as "very beautiful" because, as the source states, "it's really how you would work with generics anyway in c sharp". PurrNet doesn't force a special, networked way of thinking about generics; it lets you write network code that feels natural and efficient. This approach is ideal for features like:

* Inventory Systems: A single AddItem RPC could handle any type of ItemData.
* Event Handlers: A universal TriggerEvent<T> RPC could pass any custom event payload.
* Data Synchronization: A generic UpdateValue<T> method could sync various player stats without needing a separate RPC for each one.

Having explored how to make RPCs flexible in the data they carry, we will now examine how to make them flexible in their control flow by enabling them to return values asynchronously.

5.0 Awaitable RPCs: Mastering Asynchronous Request-Response Patterns

A common challenge in network programming involves making a request to a remote machine and waiting for a specific response before continuing local execution. Traditionally, this required complex callback systems or state management. PurrNet's Awaitable RPCs provide an elegant, modern solution using C#'s async/await pattern.

An Awaitable RPC (also known as a Returnable RPC) is an RPC method that returns a Task<T>, where T is the expected return type. By marking both the RPC and the calling method as async, developers can use the await keyword to cleanly pause execution until a value is returned from the remote destination.

Code Implementation Example

This complete example demonstrates a client asking the server to validate an integer. The client's code pauses execution until the server processes the request and returns a bool result.

// REQUIRED: Add this using statement at the top of your script.
using System.Threading.Tasks;

// --- Server-Side RPC Definition (within a NetworkBehaviour) ---
[ServerRPC(requireOwnership = false)]
private async Task<bool> MyAwaitableRPC(int myInput)
{
    // Simulate a delay, e.g., for a database check or complex calculation.
    await Task.Delay(1000); // Wait for 1 second

    // Return a result based on the input.
    return myInput > 0;
}

// --- Client-Side Calling Code ---
// The calling method must be marked as async.
private async void CheckInputWithServer()
{
    Debug.Log("Asking server to validate input...");
    
    // Await the RPC call. Code execution pauses here until the server responds.
    bool myResult = await MyAwaitableRPC(1);

    Debug.Log($"Server responded. My result is: {myResult}");
}


Pro Tip: While you can manually return a task using return Task.FromResult(value), marking the RPC method as async is highly recommended. The source material highlights that using async "just makes it very much easier because you just return it how you would normally." This approach prioritizes developer ergonomics and a natural C# workflow.

Use Cases

This pattern unlocks a number of powerful and intuitive workflows that are otherwise difficult to implement cleanly:

* Server-Side Validation: A client can request to perform an action (e.g., craft an item, cast a spell) and await the server's approval (true/false) before proceeding with local visual effects or UI updates.
* Data Retrieval: A client can request a specific piece of data from the server (e.g., player stats, quest details, inventory contents) and await the response before displaying it.
* Orchestrating Complex Actions: Logic that requires a sequence of verified steps between a client and the server can be written in a simple, linear fashion, greatly improving readability and maintainability.

The individual power of these advanced RPC types is significant, but their true potential is realized when they are combined to build even more sophisticated tools.

6.0 The Power of Combination: Building Advanced, Modular Tools

The ultimate power of PurrNet's advanced RPC system lies not in using these features in isolation, but in combining them to construct sophisticated, reusable, and highly scalable networking tools. By layering these attributes, you can create single methods that are globally accessible, type-agnostic, and support asynchronous request-response patterns.

This "Frankenstein's concoction" allows you to build powerful, all-in-one network utilities that can serve multiple purposes across your entire project with minimal code.

The Combined RPC: Code and Deconstruction

The following example demonstrates an RPC that is simultaneously static, generic, and awaitable.

[ServerRPC(requireOwnership = false)]
public static async Task<bool> MyMixedRPC<T>(T data)
{
    Debug.Log($"Server received data of type: {typeof(T)}");
    // Example logic: return true if the data is an integer.
    bool result = data is int;
    return result;
}


This single method signature elegantly combines the strengths of all three advanced features:

* public static: Makes this RPC a globally accessible utility function. It can be called from any script via ClassName.MyMixedRPC(...) without needing an object reference.
* async Task<bool>: Makes the RPC awaitable. The calling code can pause and wait for a bool result to be returned from the server, enabling clean request-response logic.
* <T>(T data): Makes the RPC generic. It can accept any serializable data type as an argument, making the validation logic highly reusable for different scenarios.

Practical Application: A Universal Validation Service

This exact MyMixedRPC function could serve as the foundation for a "Universal Validation Service." From any script in the client's codebase, a developer could send any type of data—an item to be crafted, a skill to be used, a position to move to—and await a simple true/false response from the server, all with a single, reusable RPC call. This drastically reduces boilerplate code and centralizes validation logic in a clean, maintainable way.

This combinatorial power is a key differentiator for developers building complex, modern multiplayer games.

7.0 Conclusion: Writing Next-Generation Network Code with PurrNet

This document has explored three advanced RPC types in PurrNet—Static, Generic, and Awaitable—and their respective benefits: global accessibility, type-safe reusability, and elegant asynchronous control flow. While powerful on their own, their true potential is unlocked when used in combination.

By mastering these advanced RPCs, developers can move beyond simplistic network messaging and write cleaner, more powerful, and significantly more scalable multiplayer code. PurrNet empowers you to build robust, modular networking architectures in Unity that truly feel like a natural extension of modern C#. It’s this commitment to developer experience that enables you to build awesome scalable things and awesome tools with unprecedented clarity and efficiency.
