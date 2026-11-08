import "@typespec/http";
import "@typespec/openapi";
using TypeSpec.Http;
using TypeSpec.OpenAPI;

@service(#{ title: "HexletCalendar API" })
namespace HexletCalendar.Contracts;

@extension("additionalProperties", false)
model EventType {
  @minValue(1) @maxValue(2147483647) id: int32;
  @minLength(1) @maxLength(100) name: string;
  @minLength(1) @maxLength(2000) description: string;
  @minValue(15) @maxValue(540) @extension("multipleOf", 15) durationMinutes: int32;
}
@extension("additionalProperties", false)
model CreateEventType {
  @minLength(1) @maxLength(100) name: string;
  @minLength(1) @maxLength(2000) description: string;
  @minValue(15) @maxValue(540) @extension("multipleOf", 15) durationMinutes: int32;
}
@extension("additionalProperties", false)
model EventTypes { items: EventType[]; }
@extension("additionalProperties", false)
model Problem {
  type: string;
  title: string;
  status: int32;
  code: string;
}
@extension("additionalProperties", false)
model ValidationProblem { ...Problem; errors: Record<string[]>; }
model InternalError {
  @statusCode statusCode: 500;
  @header contentType: "application/problem+json";
  @body body: Problem;
}
@route("/api/event-types") @get @operationId("getEventTypes")
op getEventTypes(): { @statusCode statusCode: 200; @body body: EventTypes; } | InternalError;
@route("/api/event-types") @post @operationId("createEventType")
op createEventType(@body body: CreateEventType):
  | { @statusCode statusCode: 201; @body body: EventType; }
  | { @statusCode statusCode: 400; @header contentType: "application/problem+json"; @body body: ValidationProblem; }
  | InternalError;
@route("/api/health") @get @operationId("getHealth")
op getHealth(): { @statusCode statusCode: 200; @header contentType: "text/plain"; @body body: "Healthy"; };
