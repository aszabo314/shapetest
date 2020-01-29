open System
open Aardvark.Base
open Aardvark.Base.Incremental
open Aardvark.Base.Rendering
open Aardvark.Rendering.Text
open Aardvark.SceneGraph
open Aardvark.Application
open Aardvark.Application.Slim

[<EntryPoint;STAThread>]
let main argv = 
    Ag.initialize()
    Aardvark.Init()

    use app = new OpenGlApplication(true)
    use win = app.CreateGameWindow(8)

    let quadGeometry =
        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexArray = ([|0;1;2; 0;2;3|] :> Array),
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, [| V3f.OOO; V3f.IOO; V3f.IIO; V3f.OIO |] :> Array
                    DefaultSemantic.Colors, [| C4b.Red; C4b.Green; C4b.Blue; C4b.Yellow |] :> Array
                ]
        )

    let initialView = CameraView.lookAt (V3d(6,6,6)) V3d.Zero V3d.OOI
    let view = 
        initialView 
        |> DefaultCameraController.control win.Mouse win.Keyboard win.Time
        |> Mod.map CameraView.viewTrafo
    let proj = 
        win.Sizes 
        |> Mod.map (fun s -> Frustum.perspective 60.0 0.1 100.0 (float s.X / float s.Y))
        |> Mod.map Frustum.projTrafo


    // create shapes by drawing a Path out of PathSegments (or use predefined primitives)
    let shapes =    
        [
            ConcreteShape.circle C4b.White 0.1 (Circle2d(V2d.Zero, 1.0))
            ConcreteShape.ofPath V2d.Zero V2d.One C4b.White (Path.ofList[
                PathSegment.line V2d.OO (V2d(0.0,0.9))
                PathSegment.arcSegment (V2d(0.0,0.9)) (V2d(0.9,0.9)) (V2d(0.9,0.0))
                PathSegment.line (V2d(0.9,0.0)) V2d.OO
            ])
            ConcreteShape.ofPath V2d.Zero V2d.One C4b.White (Path.ofList[
                PathSegment.line V2d.OO (V2d(0.0,-0.9))
                PathSegment.arcSegment (V2d(0.0,-0.9)) (V2d(-0.9,-0.9)) (V2d(-0.9,0.0))
                PathSegment.line (V2d(-0.9,0.0)) V2d.OO
            ])
        ]
        
    let instancedWorldShapeSg =
        let s = 
            shapes 
            |> ShapeList.ofList 
            |> Mod.constant
        let trafos = List.init 150 (fun i -> Trafo3d.Scale(0.1) * Trafo3d.Translation(float (i % 50)+1.0, float (i / 50), 0.0))
        ASet.ofList trafos
        |> ASet.map (fun t -> (Mod.constant t),s)
        |> Sg.shapes

    let instancedBillboardSg =  
        let s = 
            shapes 
            |> ShapeList.ofList 
            // set renderStyle for billboard, set flipViewDependent to show flipped backside
            |> (fun list -> { list with renderStyle = RenderStyle.Billboard })  
            |> Mod.constant
        let trafos = List.init 150 (fun i -> Trafo3d.Scale(0.1) * Trafo3d.Translation(float (i % 50)+1.0, float (i / 50) + 4.0, 0.0))
        ASet.ofList trafos
        |> ASet.map (fun t -> (Mod.constant t),s)
        |> Sg.shapes

    let instancedFixedSizeSg =
        let s = 
            shapes 
            |> ShapeList.ofList 
            |> (fun list -> { list with renderStyle = RenderStyle.Normal })            
            |> Mod.constant
        let trafos = List.init 150 (fun i -> Trafo3d.Translation(float (i % 50)-49.0, float (i / 50), 0.0))

        let bb t token =
            let v = view.GetValue token
            let p = proj.GetValue token
            let s = win.Sizes.GetValue token
            let mvp : Trafo3d = t * v * p
            let ndc = mvp.Forward.TransformPosProj V3d.OOO
            Trafo3d.Scale(25.0) *       // pixel size goes here
            Trafo3d.Scale(2.0/float s.X, 2.0/float s.Y, 1.0) *
            Trafo3d.Translation(ndc)

        ASet.ofList trafos
        |> ASet.map (fun t -> Mod.custom (bb t),s)
        |> Sg.shapes

    let quad =
        quadGeometry 
            |> Sg.ofIndexedGeometry
            |> Sg.effect [
                DefaultSurfaces.trafo |> toEffect
                DefaultSurfaces.vertexColor |> toEffect
               ]

    let sg =
        [
            quad
            instancedBillboardSg
            instancedWorldShapeSg
        ]   
        |> Sg.ofList
        |> Sg.viewTrafo view
        |> Sg.projTrafo proj 
        |> Sg.andAlso instancedFixedSizeSg

    let task =
        app.Runtime.CompileRender(win.FramebufferSignature, sg)

    win.RenderTask <- task
    win.Run()
    0