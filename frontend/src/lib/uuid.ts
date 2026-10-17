const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Ids in the URL are UUIDs; anything else is rejected so a crafted segment such as ".." cannot change an API path. */
export const isUuid = (value: string) => UUID.test(value);
