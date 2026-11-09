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
@format("date") @pattern("^\\d{4}-\\d{2}-\\d{2}$")
scalar CalendarDate extends string;
@format("date-time") @pattern("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:00Z$")
scalar SlotTimestamp extends string;
@format("date-time") @pattern("^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}Z$")
scalar ServerTimestamp extends string;
@extension("additionalProperties", false)
model AvailabilitySlot { startAt: SlotTimestamp; endAt: SlotTimestamp; isAvailable: boolean; }
@extension("additionalProperties", false)
model AvailabilityDay { date: CalendarDate; slots: AvailabilitySlot[]; }
@extension("additionalProperties", false)
model Availability {
  @minValue(1) @maxValue(2147483647) eventTypeId: int32;
  @pattern("^Europe/Moscow$") timeZone: string;
  serverNow: ServerTimestamp;
  windowStartDate: CalendarDate;
  windowEndDate: CalendarDate;
  @minItems(14) @maxItems(14) days: AvailabilityDay[];
}
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
@route("/api/event-types/{id}/availability") @get @operationId("getAvailability")
op getAvailability(@path id: string):
  | { @statusCode statusCode: 200; @body body: Availability; }
  | { @statusCode statusCode: 400; @header contentType: "application/problem+json"; @body body: ValidationProblem; }
  | { @statusCode statusCode: 404; @header contentType: "application/problem+json"; @body body: Problem; }
  | InternalError;
@route("/api/health") @get @operationId("getHealth")
op getHealth(): { @statusCode statusCode: 200; @header contentType: "text/plain"; @body body: "Healthy"; };
