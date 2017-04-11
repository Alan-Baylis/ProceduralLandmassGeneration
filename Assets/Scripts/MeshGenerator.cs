﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MeshGenerator {

	public static MeshData GenerateTerrainMesh(float[,] heightMap, float heightMultiplier, AnimationCurve _heightCurve, int levelOfDetail, bool useFlatShading)
	{
		AnimationCurve heightCurve = new AnimationCurve(_heightCurve.keys);

		int meshSimplificationIncrement = (levelOfDetail == 0) ? 1 : levelOfDetail * 2;

		int borderedSize = heightMap.GetLength(0);
		int meshSize = borderedSize - 2 * meshSimplificationIncrement;
		int meshSizeUnsimplified = borderedSize - 2;

		float topLeftX = (meshSizeUnsimplified - 1) / -2f;
		float topLeftZ = (meshSizeUnsimplified - 1) / 2f;

		int verticesPerLine = (meshSize - 1) / meshSimplificationIncrement + 1;

		MeshData meshData = new MeshData(verticesPerLine, useFlatShading);
		int[,] vertexIndicesMap = new int[borderedSize,borderedSize];
		int meshVertexIndex = 0;
		int borderVertexIndex = -1;

		for (int y = 0; y < borderedSize; y += meshSimplificationIncrement)
		{
			for (int x = 0; x < borderedSize; x += meshSimplificationIncrement)
			{
				bool isBorderVertex = y == 0 || y == borderedSize - 1 || x == 0 || x == borderedSize - 1;

				if (isBorderVertex)
				{
					vertexIndicesMap[x, y] = borderVertexIndex;
					borderVertexIndex--;
				}
				else
				{
					vertexIndicesMap[x, y] = meshVertexIndex;
					meshVertexIndex++;
				}
			}
		}

		for (int y = 0; y < borderedSize; y+= meshSimplificationIncrement)
		{
			for (int x = 0; x < borderedSize; x += meshSimplificationIncrement)
			{
				int vertexIndex = vertexIndicesMap[x, y];
				Vector2 percent =  new Vector2((x - meshSimplificationIncrement) / (float)meshSize, (y-meshSimplificationIncrement) / (float)meshSize);
				float height = heightCurve.Evaluate(heightMap[x, y]) * heightMultiplier;
				Vector3 vertexPosition = new Vector3(topLeftX + percent.x * meshSizeUnsimplified, height, topLeftZ - percent.y * meshSizeUnsimplified);

				meshData.AddVertex(vertexPosition, percent, vertexIndex);

				//we're ignoring the right and bottom edge vertices of the map
				if (x < borderedSize-1 && y < borderedSize - 1)
				{
					int a = vertexIndicesMap[x, y];
					int b = vertexIndicesMap[x + meshSimplificationIncrement, y];
					int c = vertexIndicesMap[x, y + meshSimplificationIncrement];
					int d = vertexIndicesMap[x + meshSimplificationIncrement, y + meshSimplificationIncrement];

					meshData.AddTriangle(a,d,c);
					meshData.AddTriangle(d,a,b);
				}

				vertexIndex++;
			}
		}
		meshData.ProcessMesh();

		return meshData;
	}
}

public class MeshData
{
	Vector3[] vertices;
	int[] triangles;
	// uv map so that we can add textures to our mesh
	Vector2[] uvs;
	Vector3[] bakedNormals;

	Vector3[] borderVertices;
	int[] borderTriangles;

	int triangleIndex;
	int borderTriangleIndex;

	bool useFlatShading;

	public MeshData(int verticesPerLine, bool useFlatShading)
	{
		this.useFlatShading = useFlatShading;
		// v = w * h
		vertices = new Vector3[verticesPerLine * verticesPerLine];
		// percentage between 0 and 1
		uvs = new Vector2[verticesPerLine * verticesPerLine];
		// t = (w - 1) * (h - 1) * 6 
		// (because each square of our matrix is made by 2 triangles of 3 vertices each ;)
		triangles = new int[(verticesPerLine - 1) * (verticesPerLine-1) * 6];

		borderVertices = new Vector3[verticesPerLine * 4 + 4];
		//( 24 because 6 * 4 * number of vertices per line )
		borderTriangles = new int[24 * verticesPerLine];
	}

	public void AddVertex(Vector3 vertexPosition, Vector2 uv, int vertexIndex)
	{
		if(vertexIndex < 0)
		{
			borderVertices[-vertexIndex - 1] = vertexPosition;
		}
		else
		{
			vertices[vertexIndex] = vertexPosition;
			uvs[vertexIndex] = uv;
		}
	}


	public void AddTriangle(int a, int b, int c)
	{
		// triangle beyond the border
		if ( a < 0 || b < 0 || c < 0)
		{
			borderTriangles[borderTriangleIndex] = a;
			borderTriangles[borderTriangleIndex + 1] = b;
			borderTriangles[borderTriangleIndex + 2] = c;
			borderTriangleIndex += 3;
		}
		// regular triangle
		else
		{
			triangles[triangleIndex] = a;
			triangles[triangleIndex + 1] = b;
			triangles[triangleIndex + 2] = c;
			triangleIndex += 3;
		}
	}

	Vector3[] CalculateNormals()
	{
		Vector3[] vertexNormals = new Vector3[vertices.Length];
		int triangleCount = triangles.Length / 3;

		//looping throuh regular triangles
		for (int i =0; i <triangleCount; i++)
		{
			int normalTriangleIndex = i * 3;
			int vertexIndexA = triangles[normalTriangleIndex];
			int vertexIndexB = triangles[normalTriangleIndex + 1];
			int vertexIndexC = triangles[normalTriangleIndex + 2];

			Vector3 triangleNormal = SurfaceNormalFrontIndices(vertexIndexA, vertexIndexB, vertexIndexC);
			vertexNormals[vertexIndexA] += triangleNormal;
			vertexNormals[vertexIndexB] += triangleNormal;
			vertexNormals[vertexIndexC] += triangleNormal;
		}

		// looping through triangles beyond the borders
		int borderTriangleCount = borderTriangles.Length / 3;
		for (int i = 0; i < borderTriangleCount; i++)
		{
			int normalTriangleIndex = i * 3;
			int vertexIndexA = borderTriangles[normalTriangleIndex];
			int vertexIndexB = borderTriangles[normalTriangleIndex + 1];
			int vertexIndexC = borderTriangles[normalTriangleIndex + 2];

			Vector3 triangleNormal = SurfaceNormalFrontIndices(vertexIndexA, vertexIndexB, vertexIndexC);
			if (vertexIndexA >= 0)
			{
				vertexNormals[vertexIndexA] += triangleNormal;
			}
			if (vertexIndexB >= 0)
			{
				vertexNormals[vertexIndexB] += triangleNormal;
			}
			if (vertexIndexC >= 0)
			{
				vertexNormals[vertexIndexC] += triangleNormal;
			}

		}

		for (int i = 0; i < vertexNormals.Length; i++)
		{
			vertexNormals[i].Normalize();
		}
		return vertexNormals;
	}

	Vector3 SurfaceNormalFrontIndices(int indexA, int indexB, int indexC)
	{
		Vector3 pointA = (indexA < 0) ? borderVertices[-indexA - 1] : vertices[indexA];
		Vector3 pointB = (indexB < 0) ? borderVertices[-indexB - 1] : vertices[indexB];
		Vector3 pointC = (indexC < 0) ? borderVertices[-indexC - 1] : vertices[indexC];

		Vector3 sideAB = pointB - pointA;
		Vector3 sideAC = pointC - pointA;
		return Vector3.Cross(sideAB, sideAC).normalized;
	}

	public void ProcessMesh()
	{
		if (useFlatShading)
		{
			FlatShading();
		}
		else
		{
			// get the normals to be consistant when sharing across the
			// edges of Mesh chunks
			BakedNormals();
		}
	}

	private void BakedNormals()
	{
		bakedNormals = CalculateNormals();
	}

	void FlatShading()
	{
		Vector3[] flatShadedVertices = new Vector3[triangles.Length];
		Vector2[] flatShadedUvs = new Vector2[triangles.Length];

		for (int i = 0; i < triangles.Length; i++)
		{
			flatShadedVertices[i] = vertices[triangles[i]];
			flatShadedUvs[i] = uvs[triangles[i]];
			triangles[i] = i;
		}

		vertices = flatShadedVertices;
		uvs = flatShadedUvs;
	}

	public Mesh CreateMesh()
	{
		Mesh mesh = new Mesh();
		mesh.vertices = vertices;
		mesh.triangles = triangles;
		mesh.uv = uvs;
		if (useFlatShading)
		{
			mesh.RecalculateNormals();
		}
		else
		{
			mesh.normals = bakedNormals;
		}
		return mesh;
	}

}
