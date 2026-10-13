const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Ids in the URL are UUIDs. Anything else is rejected before it reaches an API path, so a crafted segment
 * such as ".." cannot turn /api/workspaces/{id} into a different API path.
 */
export const isUuid = (value: string) => UUID.test(value);
