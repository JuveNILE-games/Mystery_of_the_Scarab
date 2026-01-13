Creating Worlds with Code: A Technical White Paper on the Evolution of Procedural Generation in Game Development

1.0 Introduction: Beyond Randomness — The Strategic Value of Procedural Content Generation

In modern game development, Procedural Content Generation (PCG) has evolved far beyond a simple tool for creating randomness. Its primary purpose is to strategically simplify work and automatically generate usable content with minimal user input, empowering developers to accelerate content creation, build vast and diverse game worlds, and enable novel gameplay experiences. By leveraging algorithms to construct environments, items, and even narratives, development teams can achieve a scale and variety that would be impossible through manual creation alone.

The sheer scale and impact of modern PCG are staggering. In No Man's Sky, algorithms are responsible for generating a universe featuring over 18 quintillion unique planets, each with its own procedurally generated solar system, ships, and creatures. Similarly, the indie phenomenon Minecraft utilizes PCG to create a world spanning nine hundred million square kilometers, offering players a seemingly infinite canvas for exploration and creativity. These examples demonstrate that PCG is not merely a technical shortcut but a foundational technology for creating expansive and endlessly replayable digital experiences.

The objective of this white paper is to chart the evolution of key PCG techniques used in the games industry. We will begin with a performance analysis of foundational dungeon generation algorithms, establishing a baseline for computational efficiency. From there, we will progress to advanced constraint-based systems like Wave Function Collapse, which enable the creation of aesthetically coherent and complex structures. Finally, we will culminate in a discussion on the hybrid design philosophy required to create procedural content with artistic "soul," blending algorithmic power with handcrafted artistry.

This journey begins with the classic algorithms that underpin many modern PCG systems, providing the essential building blocks for creating worlds with code.


--------------------------------------------------------------------------------


2.0 Foundational Techniques: A Comparative Analysis of Classic Dungeon Generation

Understanding the performance and output characteristics of foundational PCG algorithms is a critical first step for any developer or technical artist. These classic techniques, often used for creating room-based dungeons, provide a perfect testbed for comparing different algorithmic approaches. By analyzing their CPU usage, execution time, and memory footprint, we can make informed decisions about which tool is best suited for a given design goal, whether that goal is raw performance, structural complexity, or aesthetic variety.

Across the source materials, four primary dungeon generation algorithms are discussed, each with a distinct methodology for creating level layouts:

* Binary Space Partitioning (BSP): This technique recursively divides a space into smaller areas, creating a tree-like structure that can be used to place rooms and ensure they do not overlap.
* Depth-First Search (DFS): A classic graph traversal algorithm that explores as far as possible along each branch before backtracking. While its specific dungeon generation implementation is not detailed in the source material, it was included in the benchmark where it proved highly efficient.
* 2D Delaunay Triangulation (DT 2D): This method treats each potential room as a point in 2D space and constructs a mesh of triangles connecting them. The edges of this mesh then represent potential hallways, forming a network of interconnected paths.
* Cellular Automata: Often used to generate more organic, cave-like structures, this technique works iteratively by changing the state of a tile (e.g., from wall to floor) based on the state of its neighbors, resulting in natural-looking formations.

Performance Benchmark Analysis¹

A performance benchmark study conducted by Filip Michael provides a quantitative comparison of several of these foundational techniques. The experiment was run in the Unity game engine (version 2022.3.54f1) and compared four algorithms: Binary Space Partitioning (BSP), Depth-First Search (DFS), 2D Delaunay Triangulation (DT 2D), and a 3D variant (DT 3D). The tests were conducted across two different hardware configurations—a modern system ("W11") and an older one ("W10")—to measure CPU usage, execution time, and RAM usage when generating dungeons of varying sizes.

The following tables synthesize the key performance findings for generating 100-room dungeons on the modern W11 hardware, based on data presented in the study.

Table 1: CPU Usage Comparison (100 Rooms, W11 Hardware)

Algorithm	Median CPU Usage (%)
Depth-First Search (DFS)	7%
2D Delaunay Triangulation (DT 2D)	20%
Binary Space Partitioning (BSP)	48%
3D Delaunay Triangulation (DT 3D)	99%

Table 2: Execution Time Comparison (100 Rooms, W11 Hardware)

Algorithm	Median Execution Time (seconds)
Depth-First Search (DFS)	0.03 s
2D Delaunay Triangulation (DT 2D)	0.10 s
Binary Space Partitioning (BSP)	0.36 s
3D Delaunay Triangulation (DT 3D)	130.0 s

Table 3: RAM Usage Comparison (100 Rooms, W11 Hardware)

Algorithm	Average RAM Usage (MB)
Depth-First Search (DFS)	0.105 MB
Binary Space Partitioning (BSP)	1.35 MB
2D Delaunay Triangulation (DT 2D)	5.93 MB
3D Delaunay Triangulation (DT 3D)	1650.0 MB

¹ Performance data for CPU Usage and Execution Time are median values estimated from the box plots for W11 hardware in the source study. RAM Usage data is based on precise average values provided in the study's bar charts.

Interpretation of Results

The data clearly demonstrates that for raw performance, Depth-First Search (DFS) was the superior choice across all categories, exhibiting exceptional efficiency by requiring nearly 7 times less CPU and 12 times less RAM than the next-best algorithm, BSP. At the other end of the spectrum, the 3D Delaunay Triangulation (DT 3D) showed severe performance degradation, with CPU and RAM usage skyrocketing on larger dungeons, making it effectively unusable for practical application. The study also concluded that hardware differences primarily affected execution time, with little to no significant impact on CPU percentage or RAM usage. For a developer, this means that while a faster machine can generate content more quickly, an inefficient algorithm will remain a bottleneck regardless of the hardware.

While these foundational algorithms are powerful for generating basic layouts, creating more complex, organic, and aesthetically pleasing structures requires a more advanced approach—one that relies not on simple division or pathfinding, but on solving a complex set of artistic and logical constraints.


--------------------------------------------------------------------------------


3.0 The Next Wave: Constraint Solvers and Wave Function Collapse (WFC)

As procedural generation matured, the strategic goal shifted from simply creating random layouts to generating content that is coherent, complex, and aesthetically pleasing. Wave Function Collapse (WFC) represents an evolutionary step in this direction. It is an algorithm that allows developers to generate content that satisfies a predefined set of rules or constraints, resulting in outputs that feel intentional and artistically directed rather than purely chaotic.

At its core, Wave Function Collapse is a constraint-solving algorithm. The concept can be best understood through the analogy of solving a Sudoku puzzle. In Sudoku, each empty cell begins with a full "possibility space"—it could contain any number from 1 to 9. However, as you place numbers, you introduce constraints. A '5' placed in a row eliminates the possibility of any other '5' appearing in that same row, column, or 3x3 square. This single choice causes a chain reaction, reducing the possibility space for neighboring cells, which in turn constrains their neighbors, until a final, valid solution emerges. WFC operates on this same principle, using a set of rules to intelligently narrow down possibilities until a coherent result is achieved.

It is important to clarify a common point of confusion: the Wave Function Collapse algorithm used in procedural generation bears no similarity or relevance to the quantum mechanics concept of the same name.

The WFC Process in Practice

The application of WFC in the game Bad North by creator Oskar Stålberg provides a clear illustration of its process:

1. Establish Adjacency Rules: The process begins with a library of individual, handcrafted 3D chunks of terrain, or "tiles." The system scans the edges of each tile to determine which other tiles can validly connect to it. For example, a coastline tile can connect to another coastline tile, a water tile, or an inland tile, but not to a cliff face tile on its water-facing edge.
2. Initialize the Possibility Space: Next, a grid is created where each cell is filled with the entire "possibility space"—a list of every potential tile from the library that could theoretically fit in that location.
3. Collapse and Propagate: The generation is initiated by placing a single, definitive tile in one cell (for example, a house that must be defended). This action "collapses" the possibility space of that cell to a single choice. This choice then propagates constraints outward to its neighbors, eliminating incompatible tiles from their possibility spaces, which in turn affects their neighbors, and so on.
4. Iterate and Validate: The process repeats, collapsing cells one by one. The system ensures the final generated level satisfies both 'hard constraints' (rules that absolutely must be followed, like navigability for troops) and 'soft constraints' (rules that are flexible but preferred, guiding the aesthetic outcome).

This constraint-driven approach moves beyond simple randomness, enabling the creation of intricate and logical structures. The journey of its creator provides a compelling case study in how this powerful theory is adapted and evolved in real-world game development.


--------------------------------------------------------------------------------


4.0 Case Study: The Creative and Technical Journey of Oskar Stålberg

By tracing the evolution of procedural generation through the projects of developer Oskar Stålberg—from Bad North to Townscaper and his more recent experiments—we can gain critical insights into how advanced algorithms are adapted to solve different design challenges. This journey showcases a developer building upon previous work, combining and refining techniques to achieve increasingly sophisticated and artistically expressive results.

Contrasting Design Goals: Bad North vs. Townscaper

The difference between Stålberg's two most well-known titles highlights the versatility of PCG. In Bad North, a micro-strategy game, PCG was required to satisfy complex gameplay constraints. The generated islands needed to be aesthetically varied but, more importantly, functional. They had to provide sufficient coastline for invading forces to land and ensure that paths were always navigable for both player and enemy troops to move between the beach and the villages.

In contrast, Townscaper is described as an "aesthetic toy." Here, the primary goal of the PCG system is not to satisfy gameplay rules but to react to player input by creating visually pleasing and architecturally interesting structures. The constraints are artistic, not functional, focusing on generating charming seaside towns that feel organic and coherent, regardless of what the player builds.

The Technology Behind Townscaper

Townscaper represents a culmination of techniques developed and refined across Stålberg's previous projects. Its core technology is a synthesis of three key ideas:

* Irregular Grids: Inspired by his work on the game Night Call, Stålberg moved away from rigid square grids to create more natural and realistic city geometry that respects the winding ways in which real cities are built.
* Wave Function Collapse: The constraint-solving algorithm from Bad North was adapted to determine which building tiles are valid based on their neighbors in the player-created environment. This ensures architectural consistency.
* Marching Cubes: A technique from an earlier project, Brick Block, is used to place the selected building tiles in such a way that they seamlessly fit the underlying irregular grid.

The Latest Evolution: Post-Processing for Organic Results

More recently, Stålberg's work has evolved further to tackle one of the persistent challenges of tile-based PCG: the rigid, blocky look. His latest experiments utilize hybrid grids (a mix of squares and triangles) and, critically, a multi-stage post-processing pipeline to create more organic forms. After the initial tile-based structure is generated, the system applies:

1. Cell Deformation: The underlying grid cells are warped to better fit the shapes of the modules that fill them.
2. Mesh Relaxation: The vertices of the assembled mesh are algorithmically smoothed, pulling them toward their neighbors to soften hard edges and create more natural curves. This is particularly effective at removing awkward concave corners that can arise from the grid.
3. Procedural Texturing: Finally, textures are painted onto the entire structure as a single object, further breaking up the underlying tile boundaries and unifying the visual appearance.

This post-processing stage is a key solution for overcoming the "super obviously tile shapes" that can make procedurally generated environments feel artificial, bridging the gap between algorithmic construction and organic artistry.


--------------------------------------------------------------------------------


5.0 Injecting "Soul": The Art of Mixed-Initiative Design

How can developers create procedurally generated worlds that possess "soul"—that ineffable quality of charm, character, and intentionality? The answer lies not in a single perfect algorithm, but in a broader design philosophy known as the Hybrid Approach, which marries the computational power of procedural generation with the irreplaceable touch of handcrafted artistry. A powerful implementation of this philosophy is Mixed-Initiative Design, where the system acts as a direct collaborator with the user, creating worlds that are not just vast and varied, but also memorable and compelling.

The Hybrid Approach

Oskar Stålberg's work is a testament to the power of creating "space for both hand crafting and algorithms." This philosophy is critical for two reasons. First, it avoids the unwieldy complexity of trying to define an algorithm for every minute detail. Attempting to procedurally place every window, door, and flower pot would quickly become an intractable design problem. Second, and more importantly, it allows artists and designers to inject unique, handcrafted touches that give the world its distinct character and personality. The algorithm builds the robust foundation, while the human artist provides the soul.

This hybrid approach manifests in several concrete ways throughout Stålberg's projects:

* Handcrafted Tile Sets: The very foundation of the Wave Function Collapse system is a large library of tiles—sometimes numbering in the hundreds—that are meticulously built by an artist in a 3D modeling tool like Maya. Each tile is a small piece of handcrafted art, and the algorithm's job is to assemble them into a coherent whole.
* Discoverable "Recipes": In Townscaper, certain configurations of player-built blocks trigger high-priority WFC rules that generate unique, pre-designed structures like gardens, stairwells, or lighthouses. These "recipes" turn procedural rules into a delightful discovery mechanic, rewarding players for experimenting with the system's logic.
* Player as Co-Creator: Townscaper is a quintessential "mixed-initiative AI system." The player provides the high-level input—placing or removing a block—and the procedural system handles the complex architectural details, choosing the correct tiles, adding windows, and ensuring the structure remains visually consistent. This collaborative process between player and algorithm is what makes the experience so engaging and creatively satisfying.

Ultimately, the "soul" in procedural content generation emerges from this intelligent and intentional blending of algorithmic power with human creativity. It is a partnership where each side plays to its strengths, resulting in something greater than either could achieve alone.


--------------------------------------------------------------------------------


6.0 Conclusion: Guiding Principles for the Modern PCG Developer

The journey from simple dungeon generators to complex, art-driven systems like Townscaper reveals a clear set of core principles for leveraging Procedural Content Generation effectively. The modern approach is not about finding a single "best" algorithm but about developing a flexible, hybrid mindset that combines technical expertise with artistic vision. The most successful PCG systems are those that are thoughtfully designed, continuously evolved, and deeply integrated with human creativity.

The most critical takeaways from this analysis can be distilled into four guiding principles:

1. Choose the Right Tool for the Job. The optimal algorithm is entirely dependent on project needs. Highly efficient options like Depth-First Search are ideal for performance-critical tasks, while other methods like Binary Space Partitioning or Cellular Automata offer different structural and aesthetic trade-offs.
2. Embrace Constraints as a Creative Force. Advanced techniques like Wave Function Collapse demonstrate that complex, coherent worlds emerge not from pure randomness, but from a well-designed set of rules. These constraints are what transform chaos into structured, believable environments.
3. Iterate and Evolve Your Systems. The most powerful PCG solutions are not built in a vacuum. As demonstrated by Oskar Stålberg's career, they are the result of evolving, adapting, and combining techniques from one project to the next, with each iteration building on the lessons of the last.
4. Marry Algorithm with Artistry. The most compelling procedural worlds are born from a hybrid approach. The "soul" of a generated world comes from this partnership, where algorithmic systems provide the vast foundation and handcrafted details, from tile sets to post-processing rules, provide the character and charm.

As game worlds continue to grow in scale and complexity, the role of procedural generation will only become more vital. The future of the field belongs to the developers and artists who can master this blend of technical skill and artistic vision, using code not just to build worlds, but to breathe life into them.
