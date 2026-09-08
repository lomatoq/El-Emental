using UnityEngine;
namespace Elemental.Presentation.VFX
{
    // One shared shallow curved sheet per emitter. Never allocates per particle/frame.
    public static class SurfaceDustMesh
    {
        public static Mesh Create()
        {
            const int segments=8, row=segments+1;
            var vertices=new Vector3[row*row];var uv=new Vector2[vertices.Length];
            var triangles=new int[segments*segments*6];
            for(int y=0;y<row;y++)for(int x=0;x<row;x++)
            {
                float u=x/(float)segments,v=y/(float)segments;
                float r2=(u-.5f)*(u-.5f)*4+(v-.5f)*(v-.5f)*4;
                float dome=Mathf.Max(0,1-r2);
                vertices[y*row+x]=new Vector3(u-.5f,v-.5f,.045f*dome*(.7f+.3f*Mathf.Sin(u*7+v*4)));
                // Exact atlas-cell borders can wrap to the next tile after frac().
                uv[y*row+x]=new Vector2(Mathf.Lerp(.003f,.997f,u),Mathf.Lerp(.003f,.997f,v));
            }
            int n=0;
            for(int y=0;y<segments;y++)for(int x=0;x<segments;x++)
            {
                int a=y*row+x,b=a+1,c=a+row,d=c+1;
                triangles[n++]=a;triangles[n++]=b;triangles[n++]=d;
                triangles[n++]=a;triangles[n++]=d;triangles[n++]=c;
            }
            var mesh=new Mesh{name="Curved surface dust",hideFlags=HideFlags.DontSave};
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
