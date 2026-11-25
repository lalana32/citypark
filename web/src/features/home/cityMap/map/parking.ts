/**
 * Parking locations (map coordinates). Occupied lots sit right beside the drivers'
 * routes, so drivers pass them (and they flash "full"). Free lots are deliberately
 * placed away from every route: the drivers never find them.
 *
 * Keep them roughly within y ≈ 175–825 so the hero framing stays tight.
 */
export interface ParkingLot {
  id: string
  x: number
  y: number
  /** Rotation in degrees: 90 beside vertical streets, the road angle beside diagonals. */
  rotate?: number
  free?: boolean
}

/*
 * The lots in the open (right-hand) part of the hero are spread in three staggered rows
 * (top: y ≈ 250–420, middle: y ≈ 470, bottom: y ≈ 690–720) so no area looks crowded.
 * The top band has no driver routes, which is exactly where the free lots go.
 */
export const PARKING_LOTS: ParkingLot[] = [
  // Top row
  { id: 'p-h2', x: 740, y: 416 }, // south side of H2, between V-C and the diagonal
  { id: 'p-free-1', x: 1084, y: 280, rotate: 90, free: true }, // west of V-K, off every route
  { id: 'p-free-2', x: 1466, y: 252, rotate: 90, free: true }, // east of V-F, off every route
  // Middle row
  { id: 'p-diag', x: 904, y: 470, rotate: 43.7 }, // north-east side of the diagonal arterial
  { id: 'p-ne', x: 1290, y: 470, rotate: -35.5 }, // south-east of the curved NE arterial
  // Bottom row
  { id: 'p-s3', x: 800, y: 716 }, // downtown, south side of S3
  { id: 'p-main', x: 1256, y: 690, rotate: 90 }, // east of V-E
  // West of the freeway (behind the headline on wide screens; on the third driver's loop)
  { id: 'p-h4', x: 380, y: 804 }, // south side of H4
  { id: 'p-s1', x: 376, y: 246 }, // north side of S1
  { id: 'p-v1', x: 204, y: 626, rotate: 90 }, // west of V1
]
