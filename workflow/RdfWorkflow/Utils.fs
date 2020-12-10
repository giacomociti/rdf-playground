module Utils

open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Update
open VDS.RDF.Parsing

type System.Collections.Generic.IEnumerable<'a> with
    member this.Single = Seq.exactlyOne this

type INode with
    member this.Uri = (this :?> IUriNode).Uri
    member this.Types = 
        let uri = UriFactory.Create RdfSpecsHelper.RdfType
        let typeNode = this.Graph.CreateUriNode uri
        this.Graph.GetTriplesWithSubjectPredicate(this, typeNode) 
        |> Seq.map (fun x -> x.Object.Uri)

let parseTurtle path =
    let graph = new Graph()
    FileLoader.Load(graph, path)
    graph



let construct (query: SparqlQuery) (input: IGraph) =
    input.ExecuteQuery query :?> IGraph

let ask (query: SparqlQuery) (input: IGraph) =
    (input.ExecuteQuery query :?> SparqlResultSet).Result

let select (query: SparqlQuery) (input: IGraph) =
    input.ExecuteQuery query :?> SparqlResultSet

let update (endpoint: RemoteUpdateProcessor) (command: SparqlUpdateCommand) =
    endpoint.ProcessCommand command

let addBindings (cmd: Commands.ModifyCommand) (args: SparqlResultSet) =
    let where = cmd.WherePattern // TODO: clone and inject args
    Update.Commands.ModifyCommand(cmd.DeletePattern, cmd.InsertPattern, where)

let updateWithParams
    (endpoint: Update.RemoteUpdateProcessor) 
    (command: Update.Commands.ModifyCommand) 
    (arguments: SparqlQuery) 
    (input: IGraph) =
    select arguments input
    |> addBindings command
    |> endpoint.ProcessModifyCommand