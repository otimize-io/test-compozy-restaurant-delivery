/**
 * Shared client-side models mirroring the gateway REST contract and the SignalR payload.
 * The app talks ONLY to the gateway (ADR-005); these shapes match the gateway endpoints exactly.
 */

/** A demo role selected by the role switcher and sent as the `X-Demo-Role` header (ADR-002). */
export type DemoRole = 'consumer' | 'restaurant' | 'driver';

/** A pre-seeded demo identity returned by `GET /api/demo/roles`. */
export interface DemoIdentity {
  role: DemoRole;
  userId: string;
  displayName: string;
}

/** A geographic point used for the restaurant location on order placement. */
export interface GeoPoint {
  lat: number;
  lng: number;
}

/** A restaurant summary returned by search (`GET /api/restaurants?q=`). */
export interface Restaurant {
  id: string;
  name: string;
  cuisine: string;
  description?: string;
  location?: GeoPoint;
}

/** A single menu item (`GET /api/restaurants/{id}/menu`). */
export interface MenuItem {
  id: string;
  restaurantId?: string;
  name: string;
  description?: string;
  price: number;
}

/** A line in the cart: a menu item plus a quantity. */
export interface CartLine {
  item: MenuItem;
  quantity: number;
}

/** An order line as sent to the gateway (`POST /api/orders`). */
export interface OrderItemDto {
  itemId: string;
  name: string;
  quantity: number;
  unitPrice: number;
}

/** The body of `POST /api/orders`. */
export interface PlaceOrderRequest {
  consumerId: string;
  restaurantId: string;
  items: OrderItemDto[];
  restaurantLocation: GeoPoint;
}

/** The `201` response of `POST /api/orders`. */
export interface PlaceOrderResponse {
  orderId: string;
  correlationId: string;
}

/** The body of `POST /api/payments/callback` (mock PSP settlement). */
export interface PaymentCallbackRequest {
  orderId: string;
  outcome: 'settle' | 'decline';
}

/** The `GET /api/orders/{id}` response (status is an int enum on the server). */
export interface OrderSummary {
  orderId: string;
  status: number;
  total: number;
}

/** The `GET /api/orders/{id}/status` response — the 5-stage tracking view. */
export interface OrderStatus {
  orderId: string;
  stage: number;
  stageName: string;
}

/** The SignalR push payload (`OrdersHub.OrderStatusChanged`). Field names match the server record. */
export interface OrderStatusChanged {
  orderId: string;
  stage: number;
  stageName: string;
}

/** The numeric tracking stages the gateway emits (mirrors GatewayStage). */
export const TrackingStage = {
  Unknown: 0,
  OrderPlaced: 1,
  Preparing: 2,
  DriverAssigned: 3,
  OutForDelivery: 4,
  Delivered: 5,
  Refunded: 99,
} as const;

/**
 * Order lifecycle status codes as serialized by the Order service (`OrderStatus` enum, int over the wire).
 * Mirrors the server enum order exactly; used by the restaurant queue and driver assignment views.
 */
export const OrderStatusCode = {
  Placed: 0,
  AwaitingPayment: 1,
  Paid: 2,
  Accepted: 3,
  Preparing: 4,
  ReadyForPickup: 5,
  DriverAssigned: 6,
  PickedUp: 7,
  Delivered: 8,
  Faulted: 9,
  NoDriverRefunded: 10,
} as const;

/** Human-readable labels for the order status codes (functional-clean restaurant/driver UIs). */
export const ORDER_STATUS_LABELS: Readonly<Record<number, string>> = {
  [OrderStatusCode.Placed]: 'Placed',
  [OrderStatusCode.AwaitingPayment]: 'Awaiting payment',
  [OrderStatusCode.Paid]: 'Paid',
  [OrderStatusCode.Accepted]: 'Accepted',
  [OrderStatusCode.Preparing]: 'Preparing',
  [OrderStatusCode.ReadyForPickup]: 'Ready for pickup',
  [OrderStatusCode.DriverAssigned]: 'Driver assigned',
  [OrderStatusCode.PickedUp]: 'Picked up',
  [OrderStatusCode.Delivered]: 'Delivered',
  [OrderStatusCode.Faulted]: 'Faulted',
  [OrderStatusCode.NoDriverRefunded]: 'Refunded (no driver)',
};

/**
 * A single row in the restaurant order board (`GET /api/restaurant/orders`). Carries the assigned driver's
 * name and ETA once Dispatch has matched one (null before assignment) so the restaurant can follow the
 * order through pickup and delivery.
 */
export interface RestaurantQueueItem {
  orderId: string;
  status: number;
  total: number;
  correlationId: string;
  driverName?: string | null;
  etaMinutes?: number | null;
}

/**
 * The restaurant order board grouped into the columns the restaurant view renders so it can follow each
 * order end to end (`GET /api/restaurant/orders`): New (Paid), Cooking (Accepted/Preparing), AwaitingDriver
 * (ReadyForPickup or a driver assigned and heading over), OutForDelivery (PickedUp), Delivered (recent).
 */
export interface RestaurantQueueResponse {
  new: RestaurantQueueItem[];
  cooking: RestaurantQueueItem[];
  awaitingDriver: RestaurantQueueItem[];
  outForDelivery: RestaurantQueueItem[];
  delivered: RestaurantQueueItem[];
}

/**
 * A row in the consumer's order-tracking area (`GET /api/consumer/orders/{consumerId}`): one of the
 * consumer's orders with its live status and, once a driver is assigned, the driver and ETA.
 */
export interface ConsumerOrderItem {
  orderId: string;
  status: number;
  total: number;
  restaurantId: string;
  createdAt: string;
  driverName?: string | null;
  etaMinutes?: number | null;
}

/** A driver assignment row (`GET /api/driver/assignments`): an assigned, not-yet-delivered order. */
export interface DriverAssignmentItem {
  orderId: string;
  status: number;
  driverId: string;
  driverName: string;
  etaMinutes: number;
  correlationId: string;
}

/** A display step in the 5-stage consumer tracking bar (PRD F8). */
export interface TrackingStep {
  stage: number;
  label: string;
}

/** The fixed 5-stage tracking bar (PRD F8 / ADR-007). */
export const TRACKING_STEPS: readonly TrackingStep[] = [
  { stage: TrackingStage.OrderPlaced, label: 'Order placed' },
  { stage: TrackingStage.Preparing, label: 'Preparing' },
  { stage: TrackingStage.DriverAssigned, label: 'Driver assigned / en route' },
  { stage: TrackingStage.OutForDelivery, label: 'Out for delivery' },
  { stage: TrackingStage.Delivered, label: 'Delivered' },
];
