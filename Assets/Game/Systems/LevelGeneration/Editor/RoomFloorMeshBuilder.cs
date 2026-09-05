using System.Collections.Generic;
using UnityEngine;

namespace Game.Systems.LevelGeneration.Editor
{
    // Replaces phase-1's per-cell FloorTilePrefab instancing entirely
    // (Michael, 2026-09-04: "what if we generate our own floor mesh based on
    // the room bounds and apply the floor material to it?") — a single
    // generated mesh per room, built directly from the same CellSize used to
    // place everything else, sidesteps the whole class of bug phase-1 hit
    // (a tile prefab's mesh size / import scale / transform scale needing to
    // exactly match CellSize with zero drift). One box per cell, not merged
    // into a simplified room-shaped polygon — correctness over micro-
    // optimization for a prototype, and it trivially handles any shape
    // (irregular rooms, disconnected split pockets) without extra geometry
    // logic, since RoomFloorFloodFill already resolved cell membership.
    //
    // Each cell is a full box (top/bottom/4 sides), not a flat plane
    // (Michael, 2026-09-04: "the thickness should be more than just a plane,
    // effectively the floor is cube-like") — side faces between two adjacent
    // floor cells end up fully hidden inside the slab and are simply wasted
    // triangles, never a visible or correctness problem, so this doesn't
    // bother tracking cell adjacency to skip them.
    public static class RoomFloorMeshBuilder
    {
        // `wallCells`: needed to fix a real gap found live, 2026-09-04 —
        // ShellAlignmentPass deliberately shifts a thin wall piece flush
        // against the OUTWARD edge of its own cell (a calibrated, intentional
        // design for wall/corner alignment), leaving the INWARD half of that
        // cell — the half facing the room — completely empty of any wall
        // geometry. Floor stopping exactly at the true cell boundary is
        // therefore geometrically "correct" by cell classification but
        // leaves a real visible gap up to half a cell wide (confirmed live:
        // a wall's near face measured at its cell's exact center) between the
        // floor's edge and the wall's actual visible face — invisible before
        // because no floor existed yet to reveal the boundary. Fixed by
        // extending a floor cell's edge a full cellSize further whenever that
        // specific neighbor is a wall cell (reaching past any wall's near
        // face regardless of its thickness/shift), but never when the
        // neighbor is another floor cell of the same mesh — two coplanar
        // overlapping floor quads Z-fight, so only extend into wall/void
        // territory, never into more floor.
        // `existingMesh`: pass the previously-baked Mesh asset (if any) to
        // repopulate it in place rather than returning a fresh, unsaved Mesh
        // — needed so a re-bake keeps the SAME asset identity (GUID/fileID)
        // across runs. LevelBaker persists this mesh as a real .asset file
        // (Michael, 2026-09-04: an in-memory-only Mesh has no asset identity
        // at all, so Unity refuses "Apply to Prefab" for a MeshFilter
        // referencing one — there is nothing for the prefab file to point
        // at); reusing the same Mesh object on rebake is what makes that
        // Apply-to-Prefab keep working across rebakes too, not just once.
        public static Mesh BuildRoomFloorMesh(IEnumerable<Vector2Int> cells, HashSet<Vector2Int> wallCells,
            float cellSize, FloorUvMode uvMode, float thickness, Mesh existingMesh = null)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            float half = cellSize * 0.5f;

            foreach (var cell in cells)
            {
                float cx = cell.x * cellSize;
                float cz = cell.y * cellSize;

                float minX = cx - half;
                float maxX = cx + half;
                float minZ = cz - half;
                float maxZ = cz + half;

                if (wallCells.Contains(cell + new Vector2Int(-1, 0))) minX -= cellSize;
                if (wallCells.Contains(cell + new Vector2Int(1, 0))) maxX += cellSize;
                if (wallCells.Contains(cell + new Vector2Int(0, -1))) minZ -= cellSize;
                if (wallCells.Contains(cell + new Vector2Int(0, 1))) maxZ += cellSize;

                // Top face flush with the wall base (y=0, matching every
                // wall-kit piece's own origin convention); the slab extrudes
                // downward so it never pokes up through the visible floor
                // plane or a wall's base.
                float top = 0f;
                float bottom = -thickness;

                Vector2 uvMin, uvMax;
                if (uvMode == FloorUvMode.PerCell)
                {
                    uvMin = Vector2.zero;
                    uvMax = Vector2.one;
                }
                else
                {
                    // Deliberately still divides by the cell's own (un-
                    // extended) position, not the stretched min/max — an
                    // extended edge is a thin sliver that ends up hidden
                    // under/behind the wall either way, so a touch of UV
                    // stretch there is never visible.
                    uvMin = new Vector2(minX / cellSize, minZ / cellSize);
                    uvMax = new Vector2(maxX / cellSize, maxZ / cellSize);
                }

                // Top (+Y)
                AddFace(vertices, uvs, triangles,
                    new Vector3(minX, top, minZ), new Vector3(minX, top, maxZ),
                    new Vector3(maxX, top, maxZ), new Vector3(maxX, top, minZ),
                    uvMin, uvMax);

                // Bottom (-Y) — reversed winding to face downward
                AddFace(vertices, uvs, triangles,
                    new Vector3(minX, bottom, minZ), new Vector3(maxX, bottom, minZ),
                    new Vector3(maxX, bottom, maxZ), new Vector3(minX, bottom, maxZ),
                    uvMin, uvMax);

                // +X side
                AddFace(vertices, uvs, triangles,
                    new Vector3(maxX, top, minZ), new Vector3(maxX, top, maxZ),
                    new Vector3(maxX, bottom, maxZ), new Vector3(maxX, bottom, minZ),
                    uvMin, uvMax);

                // -X side
                AddFace(vertices, uvs, triangles,
                    new Vector3(minX, top, maxZ), new Vector3(minX, top, minZ),
                    new Vector3(minX, bottom, minZ), new Vector3(minX, bottom, maxZ),
                    uvMin, uvMax);

                // +Z side
                AddFace(vertices, uvs, triangles,
                    new Vector3(maxX, top, maxZ), new Vector3(minX, top, maxZ),
                    new Vector3(minX, bottom, maxZ), new Vector3(maxX, bottom, maxZ),
                    uvMin, uvMax);

                // -Z side
                AddFace(vertices, uvs, triangles,
                    new Vector3(minX, top, minZ), new Vector3(maxX, top, minZ),
                    new Vector3(maxX, bottom, minZ), new Vector3(minX, bottom, minZ),
                    uvMin, uvMax);
            }

            var mesh = existingMesh != null ? existingMesh : new Mesh();
            mesh.name = "GeneratedRoomFloor";
            mesh.Clear();
            // 16-bit default index buffer caps a mesh at 65535 vertices
            // (~2700 cells at 24 verts/cell) — comfortably beyond any Layout
            // this generator produces today, but cheap insurance against a
            // silent triangle dropout if that ever changes.
            mesh.indexFormat = vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Four verts wound so the face normal points toward whichever
        // direction (a,b,c,d) are given in — callers pass corners already in
        // the correct winding order for the face's intended outward normal.
        private static void AddFace(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvMin, Vector2 uvMax)
        {
            int baseIndex = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            uvs.Add(new Vector2(uvMin.x, uvMin.y));
            uvs.Add(new Vector2(uvMin.x, uvMax.y));
            uvs.Add(new Vector2(uvMax.x, uvMax.y));
            uvs.Add(new Vector2(uvMax.x, uvMin.y));

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }
    }
}
