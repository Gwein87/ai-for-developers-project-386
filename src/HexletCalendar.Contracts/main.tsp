import "@typespec/http";
import "@typespec/openapi";

using TypeSpec.Http;
using TypeSpec.OpenAPI;

@service(#{ title: "Toolchain compatibility probe" })
namespace HexletCalendar.ContractProbe;

model ProbeDetail {
  note: string | null;
}

@format("date-time")
@pattern("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}([.][0-9]+)?(Z|[+-][0-9]{2}:[0-9]{2})$")
scalar ProbeTimestamp extends string;

model ProbeItem {
  @minValue(1)
  @maxValue(2147483647)
  id: int32;
  createdAt: ProbeTimestamp;
  detail: ProbeDetail;
}

@error
model ProbeProblem {
  code: string;
  message: string;
}

@route("/probe")
@get
@operationId("getProbe")
op getProbe():
  | { @statusCode statusCode: 200; @body body: ProbeItem; }
  | {
      @statusCode statusCode: 409;
      @header contentType: "application/problem+json";
      @body body: ProbeProblem;
    };
