module ConformanceCases

open Ranvier.View.Testing

let modules: (string * Conformance.Case array) array =
    [|
        "RangeChecks", RangeChecks.allCases
        "TemplateChecks", TemplateChecks.cases
        "DelegationChecks", DelegationChecks.cases
        "TestingApiChecks", TestingApiChecks.cases
        "BoundaryChecks", BoundaryChecks.cases
        "PlacementChecks", PlacementChecks.cases
        "SugarChecks", SugarChecks.cases
        "TemplateCatalogueChecks", TemplateCatalogueChecks.cases
        "UnsafeHtmlChecks", UnsafeHtmlChecks.cases
        "PendingRegionChecks", PendingRegionChecks.cases
        "OwnedAsyncChecks", OwnedAsyncChecks.cases
    |]
